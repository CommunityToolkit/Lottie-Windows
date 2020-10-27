// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    ref struct ListBuilder<T>
        where T : class
    {
        int _count;
        T[]? _array;

        public void AddItemIfNotNull(T? item)
        {
            if (!(item is null))
            {
                if (_count == 0)
                {
                    _array = new T[4];
                }

                _array![_count] = item;
                _count++;
            }
        }

        /// <summary>
        /// Sets the expected size of the list.
        /// </summary>
        public void SetCapacity(int capacity)
        {
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
            if (_array.Length == _count)
            {
                result = _array;

                // Null out _array so that any subsequent attempts to use the object
                // will cause a crash. This is necessary because we are handing out
                // the underlying array.
                _array = null;
            }
            else
            {
                result = new T[_count];
                Array.Copy(_array, result, _count);
            }

            return result;
        }
    }
}