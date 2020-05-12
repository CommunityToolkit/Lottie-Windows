// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools
{
    sealed class ListOfNeverNull<T> : IList<T>, IReadOnlyList<T>
    {
        readonly List<T> _wrapped = new List<T>();

        internal ListOfNeverNull()
        {
        }

        /// <inheritdoc/>
        public T this[int index]
        {
            get => _wrapped[index];

            set
            {
                AssertNotNull(value);
                _wrapped[index] = AssertNotNull(value);
            }
        }

        /// <inheritdoc/>
        public int Count => _wrapped.Count;

        bool ICollection<T>.IsReadOnly => ((IList<T>)_wrapped).IsReadOnly;

        /// <inheritdoc/>
        public void Add(T item)
        {
            _wrapped.Add(AssertNotNull(item));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _wrapped.Clear();
        }

        bool ICollection<T>.Contains(T item)
        {
            return _wrapped.Contains(item);
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            _wrapped.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IList<T>)_wrapped).GetEnumerator();
        }

        /// <inheritdoc/>
        public int IndexOf(T item)
        {
            return _wrapped.IndexOf(item);
        }

        /// <inheritdoc/>
        public void Insert(int index, T item)
        {
            _wrapped.Insert(index, AssertNotNull(item));
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            var result = _wrapped.Remove(item);
            return result;
        }

        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            _wrapped.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<T>)_wrapped).GetEnumerator();
        }

        static T AssertNotNull(T item)
        {
            if (item is null)
            {
                throw new ArgumentException();
            }

            return item;
        }

        /// <inheritdoc/>
        public override string ToString()
             => Count == 0
                ? "[<none>]"
                : $"[{string.Join(", ", _wrapped.Select(item => item.ToString()))}]";
    }
}
