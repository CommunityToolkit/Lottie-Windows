// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData.Optimization
{
#if PUBLIC_LottieData
    public
#endif

    struct MergeResult<T>
        where T : class
    {
        /// <summary>
        /// Indicates if some method ended succesfully and returned a non-null value.
        /// </summary>
        public bool Success => Value is not null;

        /// <summary>
        /// Value that method returned, can be null if method failed to return any value.
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// Score in range [0, 1] associated with <see cref="Value"/>.
        /// </summary>
        public double Score { get; }

        public MergeResult(T? merged, double score = 0.0)
        {
            Value = merged;
            Score = score;
        }

        public static MergeResult<T> Failed => new MergeResult<T>(null);

        public static MergeResult<T> From<TOther>(MergeResult<TOther> other)
            where TOther : class, T
        {
            return new MergeResult<T>(other.Value, other.Score);
        }
    }
}
