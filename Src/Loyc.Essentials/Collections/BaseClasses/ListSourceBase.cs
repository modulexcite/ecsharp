﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Loyc.Collections
{
	/// <summary>Helps you implement sources (read-only collections) by providing
	/// default implementations for most methods of IList(T) and
	/// IListSource(T).</summary>
	/// <remarks>
	/// You only need to implement two methods yourself:
	/// <code>
	///     public abstract int Count { get; }
	///     public abstract T TryGet(int index, ref bool fail);
	/// </code>
	/// </remarks>
	[Serializable]
	public abstract class ListSourceBase<T> : SourceBase<T>, IListSource<T>
	{
		#region IListSource<T> Members

		public abstract T TryGet(int index, ref bool fail);
		public abstract override int Count { get; }

		public T this[int index]
		{ 
			get {
				bool fail = false;
				T value = TryGet(index, ref fail);
				if (fail)
					ThrowIndexOutOfRange(index);
				return value;
			}
		}
		
		public int IndexOf(T item)
		{
			return LCInterfaces.IndexOf(this, item);
		}
		protected int ThrowIndexOutOfRange(int index)
		{
			throw new IndexOutOfRangeException(string.Format(
				"Index out of range: {0}[{1} of {2}]", GetType().Name, index, Count));
		}

		IRange<T> IListSource<T>.Slice(int start, int count)
		{
			return Slice(start, count); 
		}
		public Slice_<T> Slice(int start, int count)
		{
			return new Slice_<T>(this, start, count); 
		}

		public override IEnumerator<T> GetEnumerator()
		{
			bool fail = false;
			T value;
			int count = Count;
			int i = 0;
			for (;; ++i) {
				value = TryGet(i, ref fail);
				if (count != Count)
					throw new EnumerationException();
				if (fail)
					break;
				yield return value;
			}
			Debug.Assert(i >= Count);
		}

		#endregion

		// IList<T> was removed because it caused an ambiguity among extension methods:
		// "The call is ambiguous between LCInterfaces.TryGet(...) and ListExt.TryGet(...)"
		//#region IList<T> Members

		//T IList<T>.this[int index]
		//{
		//    get {
		//        bool fail = false;
		//        T value = TryGet(index, ref fail);
		//        if (fail)
		//            ThrowIndexOutOfRange(index);
		//        return value;
		//    }
		//    set { throw new ReadOnlyException(); }
		//}
		//void IList<T>.Insert(int index, T item)
		//{
		//    throw new ReadOnlyException();
		//}
		//void IList<T>.RemoveAt(int index)
		//{
		//    throw new ReadOnlyException();
		//}

		//#endregion
	}
}