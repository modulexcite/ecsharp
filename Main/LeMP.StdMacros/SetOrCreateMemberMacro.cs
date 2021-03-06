﻿using Loyc;
using Loyc.Collections;
using Loyc.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LeMP
{
	using S = CodeSymbols;
	using Loyc.Ecs;

	public partial class StandardMacros
	{
		static readonly Symbol _set = GSymbol.Get("#set");

		[LexicalMacro("Type Name(set Type name) {...}; Type Name(public Type name) {...}", 
			"Automatically assigns a value to an existing field, or creates a new "+
			"field with an initial value set by calling the method. This macro can "+
			"be used with constructors and methods. This macro is activated by "+
			"attaching one of the following modifiers to a method parameter: "+
			"`set, public, internal, protected, private, protectedIn, static, partial`.", 
			"#fn", "#cons", Mode = MacroMode.Passive | MacroMode.Normal)]
		public static LNode SetOrCreateMember(LNode fn, IMessageSink sink)
		{
			// Expecting #fn(Type, Name, #(args), {body})
			if (fn.ArgCount < 3 || !fn.Args[2].Calls(S.AltList))
				return null;
			var args = fn.Args[2].Args;

			VList<LNode> propOrFieldDecls = VList<LNode>.Empty;
			Dictionary<Symbol, LNode> assignments = null;
			for (int i = 0; i < args.Count; i++) {
				var arg = args[i];
				Symbol relevantAttribute, fieldName, paramName;
				LNode plainArg, propOrFieldDecl;
				if (DetectSetOrCreateMember(arg, out relevantAttribute, out fieldName, out paramName, out plainArg, out propOrFieldDecl))
				{
					if (fn.ArgCount < 4)
						return Reject(sink, arg, Localize.Localized("'{0}': to set or create a field or property, the method must have a body in braces {{}}.", relevantAttribute));

					args[i] = plainArg;
					assignments = assignments ?? new Dictionary<Symbol, LNode>();
					assignments[fieldName] = F.Id(paramName);
					if (propOrFieldDecl != null)
						propOrFieldDecls.Add(propOrFieldDecl);
				}
			}

			if (assignments != null) // if this macro has been used...
			{
				var parts = fn.Args;
				parts[2] = parts[2].WithArgs(args);
				var body = parts[3];
				
				// Ensure the method has a normal braced body
				if (!body.Calls(S.Braces)) {
					if (parts[0].IsIdNamed(S.Void))
						body = F.Braces(body);
					else
						body = F.Braces(F.Call(S.Return, body));
				}

				// In case one constructor calls another, we have to ensure that the 
				// assignments are inserted _after_ that call, and if the constructor
				// call refers to properties or fields that will be set, we must remap
				// those references onto parameters, e.g.
				//   this(public int X) { base(X); } => this(int x) { base(x); X = x; }
				var bodyStmts = body.Args;
				int indexAtWhichToDoAssignments = 0;
				if (fn.Calls(S.Constructor)) {
					LNode baseCall = bodyStmts[0, LNode.Missing];
					if (baseCall.Calls(S.Base) || baseCall.Calls(S.This)) {
						bodyStmts[0] = baseCall.ReplaceRecursive(n => {
							LNode param;
							if (n.IsId && assignments.TryGetValue(n.Name, out param))
								return param;
							return null;
						});
						indexAtWhichToDoAssignments = 1;
					}
				}

				// Insert assignment statements
				parts[3] = body.WithArgs(bodyStmts.InsertRange(indexAtWhichToDoAssignments, assignments.Select(p => {
					if (p.Key == p.Value.Name)
						return F.Call(S.Assign, F.Dot(F.@this, F.Id(p.Key)), p.Value);
					else
						return F.Call(S.Assign, F.Id(p.Key), p.Value);
				}).ToList()));

				// Return output code
				fn = fn.WithArgs(parts);
				if (propOrFieldDecls.IsEmpty)
					return fn;
				else {
					propOrFieldDecls.Add(fn);
					return F.Call(S.Splice, propOrFieldDecls);
				}
			}
			return null;
		}

		private static bool DetectSetOrCreateMember(LNode arg, out Symbol relevantAttribute, out Symbol fieldName, out Symbol paramName, out LNode plainArg, out LNode propOrFieldDecl)
		{
			relevantAttribute = null;
			fieldName = null;
			paramName = null;
			plainArg = null;
			propOrFieldDecl = null;
			LNode _, type, name, defaultValue, propArgs;
			if (EcsValidators.IsPropertyDefinition(arg, out type, out name, out propArgs, out _, out defaultValue) && propArgs.ArgCount == 0) {
				// #property(Type, Name<T>, {...})
				relevantAttribute = S.Property;
				fieldName = EcsNodePrinter.KeyNameComponentOf(name);
				paramName = ChooseArgName(fieldName);
				if (defaultValue != null) { // initializer is Args[4]
					plainArg = F.Var(type, paramName, defaultValue);
					propOrFieldDecl = arg.WithArgs(arg.Args.First(4));
				} else {
					plainArg = F.Var(type, paramName);
					propOrFieldDecl = arg;
				}
				return true;
			} else if (IsVar(arg, out type, out paramName, out defaultValue)) {
				int a_i = 0;
				foreach (var attr in arg.Attrs) {
					if (attr.IsId) {
						var a = attr.Name;
						if (a == _set
							|| a == S.Public || a == S.Internal || a == S.Protected || a == S.Private
							|| a == S.ProtectedIn || a == S.Static || a == S.Partial) {
							relevantAttribute = a;
							fieldName = paramName;
							paramName = ChooseArgName(fieldName);
							if (a == _set) {
								plainArg = F.Var(type, paramName, defaultValue).WithAttrs(arg.Attrs.Without(attr));
							} else {
								// in case of something like "[A] public params T arg = value", 
								// assume that "= value" represents a default value, not a field 
								// initializer, that [A] belongs on the field, except `params` 
								// which stays on the argument.
								plainArg = F.Var(type, paramName, defaultValue);
								propOrFieldDecl = arg;
								if (arg.Args[1].Calls(S.Assign, 2))
									propOrFieldDecl = arg.WithArgChanged(1,
										arg.Args[1].Args[0]);
								int i_params = arg.Attrs.IndexWithName(S.Params);
								if (i_params > -1) {
									plainArg = plainArg.PlusAttr(arg.Attrs[i_params]);
									propOrFieldDecl = propOrFieldDecl.WithAttrs(propOrFieldDecl.Attrs.RemoveAt(i_params));
								}
							}
							break;
						}
					}
					a_i++;
				}
				return plainArg != null;
			}
			return false;
		}

		private static bool IsVar(LNode arg,out LNode type, out Symbol name, out LNode defaultValue)
		{
			name = null;
			LNode nameNode;
			if (!EcsValidators.IsVariableDeclExpr(arg, out type, out nameNode, out defaultValue))
				return false;
			name = nameNode.Name;
			return nameNode.IsId;
		} 
		static Symbol ChooseArgName(Symbol fieldName)
		{
			char first = fieldName.Name.FirstOrDefault();
			if (first == '_' && fieldName.Name.Length > 1)
				return GSymbol.Get(fieldName.Name.Substring(1));
			char lower;
			if ((lower = char.ToLowerInvariant(first)) != first)
				return GSymbol.Get(lower + fieldName.Name.Substring(1));
			
			// NOTE: if the method is static, "this" does not exist so it's not 
			// possible to write "this.name = name;" and "name = name;" won't work, 
			// so the arg name should be different from the field name. 
			// Ignoring that corner case for now.
			return fieldName;
		}
	}
}
