// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Extension methods for <see cref="ReadOnlySpan{T}"/> to make it easier to treat them
    /// like IEnumerables.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    static class ExtensionMethods
    {
        public static bool Any<TSource>(this ReadOnlySpan<TSource> source)
        {
            return source.Length > 0;
        }

        public static bool Any<TSource>(this ReadOnlySpan<TSource> source, Func<TSource, bool> predicate)
        {
            for (var i = 0; i < source.Length; i++)
            {
                if (predicate(source[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static double Max(this ReadOnlySpan<double> source)
        {
            var result = double.MinValue;
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] > result)
                {
                    result = source[i];
                }
            }

            return result;
        }

        public static TResult[] SelectToArray<TSource, TResult>(this ReadOnlySpan<TSource> source, Func<TSource, TResult> selector)
        {
            var result = new TResult[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = selector(source[i]);
            }

            return result;
        }

        public static ReadOnlySpan<TResult> SelectToSpan<TSource, TResult>(this ReadOnlySpan<TSource> source, Func<TSource, TResult> selector)
        {
            var result = new TResult[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = selector(source[i]);
            }

            return new ReadOnlySpan<TResult>(result);
        }

        public static double Max<TSource>(this ReadOnlySpan<TSource> source, Func<TSource, double> selector)
        {
            var result = double.NegativeInfinity;

            foreach (var item in source)
            {
                var candidate = selector(item);
                if (candidate > result)
                {
                    result = candidate;
                }
            }

            return result;
        }

        public static double Min<TSource>(this ReadOnlySpan<TSource> source, Func<TSource, double> selector)
        {
            var result = double.PositiveInfinity;

            foreach (var item in source)
            {
                var candidate = selector(item);
                if (candidate < result)
                {
                    result = candidate;
                }
            }

            return result;
        }

        public static Opacity Max<TSource>(this ReadOnlySpan<TSource> source, Func<TSource, Opacity> selector)
        {
            var result = Opacity.Transparent;

            foreach (var item in source)
            {
                var candidate = selector(item);
                if (candidate > result)
                {
                    result = candidate;
                }
            }

            return result;
        }
    }
}
