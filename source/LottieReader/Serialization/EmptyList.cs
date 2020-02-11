// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    // A list that never contains any items.
    sealed class EmptyList<T> : IList<T>
    {
        internal static EmptyList<T> Singleton { get; } = new EmptyList<T>();

        T IList<T>.this[int index] { get => throw new IndexOutOfRangeException(); set => throw new NotImplementedException(); }

        int ICollection<T>.Count => 0;

        bool ICollection<T>.IsReadOnly => true;

        void ICollection<T>.Add(T item) => throw new NotSupportedException();

        void ICollection<T>.Clear() => throw new NotSupportedException();

        bool ICollection<T>.Contains(T item) => false;

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>)Array.Empty<T>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Array.Empty<T>().GetEnumerator();

        int IList<T>.IndexOf(T item) => -1;

        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();
    }
}