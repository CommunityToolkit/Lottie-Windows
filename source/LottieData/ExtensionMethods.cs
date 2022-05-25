// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
    /// <summary>
    /// Extension methods for <see cref="IReadOnlyList{T}"/>.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    static class ExtensionMethods
    {
        public static TResult[] SelectToArray<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult> selector)
        {
            var result = new TResult[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = selector(source[i]);
            }

            return result;
        }

        public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> source, int start) => Slice(source, start, source.Count - start);

        public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> source, int start, int length)
        {
            if (start == 0 && length == source.Count)
            {
                return source;
            }
            else if (length == 0)
            {
                return Array.Empty<T>();
            }
            else
            {
                return new ListSlice<T>(source, start, length);
            }
        }

        sealed class ListSlice<T> : IReadOnlyList<T>
        {
            readonly IReadOnlyList<T> _wrapped;
            readonly int _start;

            internal ListSlice(IReadOnlyList<T> source, int start, int length)
            {
                _wrapped = source;
                _start = start;
                Count = length;
            }

            public T this[int index] => _wrapped[index + _start];

            public int Count { get; }

            public IEnumerator<T> GetEnumerator() => new Enumerator(this);

            IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

            sealed class Enumerator : IEnumerator<T>
            {
                readonly ListSlice<T> _owner;
                int _index = -1;

                internal Enumerator(ListSlice<T> owner) => _owner = owner;

                public T Current => _owner[_index];

                object? IEnumerator.Current => Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    _index++;
                    return _index < _owner.Count;
                }

                void IEnumerator.Reset() => _index = -1;
            }
        }
    }
}
