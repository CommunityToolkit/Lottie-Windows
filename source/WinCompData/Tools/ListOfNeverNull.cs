// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

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

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            var oldContents = _wrapped.ToArray();
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
            if (item == null)
            {
                throw new ArgumentException();
            }

            return item;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var tName = typeof(T).Name;
            switch (Count)
            {
                case 0:
                    return $"Empty List<{tName}>";
                case 1:
                    return $"List<{tName}> with 1 item";
                default:
                    return $"List<{tName}> with {Count} items";
            }
        }
    }
}
