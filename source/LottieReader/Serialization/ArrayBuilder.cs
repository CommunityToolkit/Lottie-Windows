// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    /// <summary>
    /// Builds an array of non-null items. Designed for use on the stack,
    /// it ignores null items, and does minimal allocations.
    /// </summary>
    /// <typeparam name="T">The type of item in the array.</typeparam>
    ref struct ArrayBuilder<T>
        where T : class
    {
        int _count;
        T[]? _array;

        public void AddItemIfNotNull(T? item)
        {
            if (item != null)
            {
                if (_array is null)
                {
                    Debug.Assert(_count == 0, "Precondition");

                    // Initial count is 4 elements. This is arbitrary, but
                    // it seems to be a good tradeoff for most Lottie cases
                    // where the capacity is not known in advance.
                    _array = new T[4];
                }
                else
                {
                    if (_count == _array.Length)
                    {
                        // Grow the array.
                        var oldArray = _array;
                        _array = new T[_count * 2];
                        Array.Copy(oldArray, _array, _count);
                    }
                }

                _array[_count] = item;
                _count++;
            }
        }

        /// <summary>
        /// Sets the expected size of the array. This is just a hint - the
        /// array may end up larger or smaller than this value if a different
        /// number of items are added. Calling this is optional, but if it is
        /// called it must be called before any items are added.
        /// </summary>
        public void SetCapacity(int capacity)
        {
            // If this assert is hit, the caller has set a capacity of 0, which indicates
            // that they should not have called here seeing as they have no items to add.
            Debug.Assert(capacity > 0, "Precondition");
            Debug.Assert(_array is null, "Capacity should be set before any items are added");

            if (_array is null)
            {
                _array = new T[capacity];
            }
        }

        public T[] ToArray()
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }

            Debug.Assert(_array != null, "ToArray() can only be called once");

            T[] result;
            if (_array!.Length == _count)
            {
                result = _array;
            }
            else
            {
                result = new T[_count];
                Array.Copy(_array, result, _count);
            }

            // Null out _array as a flag to indicate that the array has already
            // been handed out, and therefore must not have any new items added
            // to it.
            _array = null;

            return result;
        }
    }
}