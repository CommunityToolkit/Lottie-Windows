// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A sequence of items.
    /// </summary>
    /// <typeparam name="T">The type of each item in the sequence.</typeparam>
#if PUBLIC_LottieData
    public
#endif
    sealed class Sequence<T> : IEquatable<Sequence<T>>, IEnumerable<T>
    {
        static readonly string ItemTypeName = typeof(T).Name;
        readonly T[] _items;
        int _hashcode;

        public Sequence(IEnumerable<T> items)
        {
            _items = items.ToArray();
        }

        /// <summary>
        /// Gets the items in the sequence.
        /// </summary>
        public ReadOnlySpan<T> Items => _items;

        /// <summary>
        /// And empty sequence.
        /// </summary>
        public static Sequence<T> Empty { get; } = new Sequence<T>(Array.Empty<T>());

        /// <inheritdoc/>
        public bool Equals(Sequence<T> other) =>
            other != null &&
            Enumerable.SequenceEqual(_items, other._items);

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var other = obj as Sequence<T>;
            return other != null && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (_hashcode == 0)
            {
                // Calculate the hashcode and cache it.
                // Hash doesn't have to be perfect, just needs to
                // be consistent, so to save some time just look at
                // the first few items.
                for (var i = 0; i < 3 && i < _items.Length; i++)
                {
                    _hashcode ^= _items[i].GetHashCode();
                }
            }

            return _hashcode;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{ItemTypeName}s: {string.Join(", ", _items)}";

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    }
}
