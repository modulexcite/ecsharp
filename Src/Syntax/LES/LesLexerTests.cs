﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using NUnit.Framework;
using Loyc;
using S = Loyc.Syntax.CodeSymbols;

namespace Loyc.Syntax.Les
{
	using TT = TokenType;

	[TestFixture]
	public class LesLexerTests
	{
		[DebuggerStepThrough] static Symbol _(string s) { return GSymbol.Get(s); }
		[DebuggerStepThrough] static T[] A<T>(params T[] list) { return list; }

		object WS = WhitespaceTag.Value;

		[Test]
		public void Basics()
		{
			Case(@"hello, world!",
				A(TT.Id, TT.Comma, TT.Spaces, TT.Id, TT.NormalOp), 
				_("hello"), _("#,"), WS, _("world"), _("#!"));
			Case(@"this is""just""1 lexer test '!'",
				A(TT.Id, TT.Spaces, TT.Id, TT.String, TT.Number, TT.Spaces, TT.Id, TT.Spaces, TT.Id, TT.Spaces, TT.SQString),
				_("this"), WS, _("is"), "just", 1, WS, _("lexer"), WS, _("test"), WS, '!');
			Case(@"12:30", A(TT.Number, TT.Colon, TT.Number), 12, _("#:"), 30);
			Case(@"c+='0'", A(TT.Id, TT.Assignment, TT.SQString), _("c"), _("#+="), '0');
			Case("// hello\n\r\n\r/* world */",
				A(TT.SLComment, TT.Newline, TT.Newline, TT.Newline, TT.MLComment));
			Case(@"{}[]()", A(TT.LBrace, TT.RBrace, TT.LBrack, TT.RBrack, TT.LParen, TT.RParen));
			Case(@"finally@@{`boom!` \bam;}", A(TT.Id, TT.At, TT.At, TT.LBrace, TT.BQString, TT.Spaces, TT.NormalOp, TT.Semicolon, TT.RBrace),
				_("finally"), _("#@"), _("#@"), null, "boom!", WS, _("bam"), _("#;"), null);
		}

		[Test]
		public void TestIdentifiers()
		{
			Case("abc_123/_0",   A(TT.Id, TT.NormalOp, TT.Id),       _("abc_123"), _("#/"), _("_0"));
			Case("is@is",        A(TT.Id, TT.Id),                    _("is"), _("is"));
			Case("\u0041\U00000062\u0063", A(TT.Id),                 _("Abc"));
			Case("No#error",     A(TT.Id),                           _("No#error"));
			Case("#error.",      A(TT.Id, TT.Dot),                   _("#error"), _("#."));
			Case("@#food:@yum",  A(TT.Id, TT.Id),                    _("#food:"), _("yum"));
			Case(@"#()\",        A(TT.Id, TT.LParen, TT.RParen, TT.NormalOp), _("#"), null, null, _(@"#\"));
			Case(@"@\@$@==>@??.",A(TT.Id, TT.Id, TT.Id, TT.Id),      _(@"\"), _("$"), _("==>"), _("??."));
			Case("@#>>@#>>=@#<<",A(TT.Id, TT.Id, TT.Id),             _("#>>"), _("#>>="), _("#<<"));
			Case(@"@0@`@\n`",    A(TT.Id, TT.Id),                    _("0"), _("@\n"));
			Case("won't prime'", A(TT.Id, TT.Spaces, TT.Id),         _("won't"), WS, _("prime'"));
		}

		[Test]
		public void TestNormalStrings()
		{
			Case(@"`Testing`""Testing""'!'", A(TT.BQString, TT.String, TT.SQString), "Testing", "Testing", '!');
			Case(@"`\a\b\f\v\`\'\""`""\a\b\f\v\`\'\""""'\0'", A(TT.BQString, TT.String, TT.SQString),
				"\a\b\f\v`\'\"", "\a\b\f\v`\'\"", '\0');
		}
		[Test]
		public void TestTQStrings()
		{
			Case("\"\"\"Hello'''', quotes!\"\"\".", A(TT.String, TT.Dot), "Hello'''', quotes!", _("#."));
			Case("'''Hello\"\"\"\", quotes!'''.", A(TT.String, TT.Dot), "Hello\"\"\"\", quotes!", _("#."));
		}

		[Test]
		public void TestShebang()
		{
			Case("#!/bin/sh\r\n// that's called a shebang!",
				A(TT.Shebang, TT.SLComment));
			Case(".#!/bin/sh",
				A(TT.Dot, TT.Id, TT.NormalOp, TT.Id, TT.NormalOp, TT.Id),
				_("#."), _("#"), _("#!/"), _("bin"), _("#/"), _("sh"));
		}

		[Test]
		public void TestIntegers()
		{
			Case("9", A(TT.Number), 9);
			Case("1337", A(TT.Number), 1337);
			Case("-1", A(TT.Number), -1);
			Case("9111222U", A(TT.Number), 9111222U);
			Case("0L", A(TT.Number), 0L);
			Case("-9111222L", A(TT.Number), -9111222L);
			Case("-1U", A(TT.NormalOp, TT.Number), _("#-"), 1U);
			Case("9_111_222", A(TT.Number), 9111222);
			Case("9_111_222_333", A(TT.Number), 9111222333);
			Case("4_111_222_333", A(TT.Number), 4111222333);
			Case("4_111_222_333U", A(TT.Number), 4111222333U);
			Case("9_111_222_333_444_555", A(TT.Number), 9111222333444555);
			Case("9_111_222_333_444_555L", A(TT.Number), 9111222333444555L);
			Case("9_111_222_333_444_555UL", A(TT.Number), 9111222333444555UL);
			Case("0x9+0x0A=0x0000_0000_13", A(TT.Number, TT.NormalOp, TT.Number, TT.Assignment, TT.Number), 0x9, _("#+"), 0x0A, _("#="), 0x13);
			Case("0b1000_0000_1000_0001_1111_1111==0x8081FF", A(TT.Number, TT.NormalOp, TT.Number), 0x8081FF, _("#=="), 0x8081FF);
			Case("0b11L0b10000000_10000001_10010010_11111111U", A(TT.Number, TT.Number), 3L, 0x808192FFU);
			Case("0b1111_10000000_10000001_10010010_11111111", A(TT.Number), 0x0F808192FF);
		}

		[Test]
		public void TestFloats()
		{
			Case("0.0", A(TT.Number), 0.0);
			Case("0.1", A(TT.Number), 0.1);
            Case("25d25f25m", A(TT.Number, TT.Number, TT.Number), 25d,25f,25m);
			Case("0.25d", A(TT.Number), 0.25d);
			Case("0.25f", A(TT.Number), 0.25f);
			Case("0.25m", A(TT.Number), 0.25m);
			Case("0.25e2", A(TT.Number), 0.25e2);
			Case("10e-20", A(TT.Number), 10e-20);
			Case("0.3e+2d", A(TT.Number), 0.3e+2d);
			Case("0.3e+2f", A(TT.Number), 0.3e+2f);
			Case("0.3e+2m", A(TT.Number), 0.3e+2m);
			Case("1234567890123456789012345678901234567890d", A(TT.Number), 1234567890123456789012345678901234567890d);
			Case("123456789012345678901234567890.1234567890123456789012345678901234567890f", A(TT.Number), 
			      123456789012345678901234567890.1234567890123456789012345678901234567890f);
			Case(".5e+2.5e+2f.5m", A(TT.Number, TT.Number, TT.Number), .5e+2, .5e+2f, .5m);
			Case("Y.5", A(TT.Id, TT.Number), _("Y"), .5);
			Case("0.1.5", A(TT.Number, TT.Number), 0.1, .5);
			Case("5.ToString", A(TT.Number, TT.Dot, TT.Id), 5, _("#."), _("ToString"));
		}
		[Test]
		public void TestHexAndBinFloats()
		{
			Case("0x0.0", A(TT.Number), 0.0);
			Case("0xF.8", A(TT.Number), 15.5);
			Case("0xF.8p+1;0xF.8p1", A(TT.Number, TT.Semicolon, TT.Number), 31, _("#;"), 31);
			Case("0xA.8p-1", A(TT.Number), 5.25);
			Case("0b101.01", A(TT.Number), 5.25);
			Case("0b101.01p0f", A(TT.Number), 5.25f);
			Case("0b101.01p2", A(TT.Number), 21.0);
			Case("0b1111_1111.1111_1111p+8", A(TT.Number), (double)0xFFFF);
			Case("0b.1p-2", A(TT.Number), 0.125);
			Case("0b.1p-2f", A(TT.Number), 0.125f);
		}

		[Test]
		public void TestSymbols()
		{
			Case(@"@@public@@is@@A@@`common\\word`@@around@@here",
				A(TT.Symbol, TT.Symbol, TT.Symbol, TT.Symbol, TT.Symbol, TT.Symbol),
				_("public"), _("is"), _("A"), _(@"common\word"), _("around"), _("here"));
			Case(@"@@+-*/", A(TT.Symbol), _("+-*/"));
			Case(@"@@+-/**/", A(TT.Symbol, TT.MLComment), _("+-"), WS);
		}

		const string ERROR = "ERROR";

		[Test]
		public void EofInToken()
		{
			Case(@"""", A(TT.String), ERROR);
			Case(@"'", A(TT.SQString), ERROR);
			Case(@"`", A(TT.BQString), ERROR);
			Case(@"\", A(TT.NormalOp), _(@"#\"));
			Case(@"\\", A(TT.SuffixOp), _(@"\"));
			Case(@"@", A(TT.At), _(@"#@"));
			Case(@"@@", A(TT.At, TT.At));
			Case(@"2.0e+", A(TT.Number), ERROR);
		}

		[Test]
		public void TestErrors()
		{
			//Case("\0",              A(TT.Error), ERROR);
			//Case("\x07",            A(TT.Error), ERROR);
			Case("x=\"Hello\n",     A(TT.Id, TT.Assignment, TT.String, TT.Newline), _("x"), _("#="), ERROR, WS);
			Case("'\n'o''pq\n?''",  A(TT.SQString, TT.Newline, TT.SQString, TT.SQString, TT.Newline, TT.NormalOp, TT.SQString),
			                        ERROR, WS, 'o', ERROR, WS, _("#?"), ERROR);
			Case("'abc'",           A(TT.SQString), ERROR);
			Case("0x!0b",           A(TT.Number, TT.NormalOp, TT.Number), ERROR, _("#!"), ERROR);
			Case("`weird\nnewline", A(TT.BQString, TT.Newline, TT.Id), ERROR, WS, _("newline"));
			Case("0xFF_0000_0000U", A(TT.Number), ERROR);
			Case("0xFFFF_FFFF_0000_0000L", A(TT.Number), ERROR);
			Case("0x1_FFFF_FFFF_0000_0000", A(TT.Number), ERROR);
		}

		void Case(string input, TokenType[] tokenTypes, params object[] values)
		{
			Debug.Assert(values.Length <= tokenTypes.Length);
			
			bool error = false;
			var lexer = new LesLexer(input, (_, msg) => { Trace.WriteLine(msg); error = true; });

			int index = 0;
			for (int i = 0; i < tokenTypes.Length; i++)
			{
				error = false;
				Token token = lexer.NextToken().Value;
				Assert.AreEqual(index, token.StartIndex);
				Assert.AreEqual(tokenTypes[i], token.Type);
				if (i < values.Length) {
					Assert.AreEqual(values[i] == (object)ERROR, error);
					if (!error)
						Assert.AreEqual(values[i], token.Value);
				}
				index += token.Length;
			}
			Assert.That(lexer.NextToken() == null);
		}
	}
}
