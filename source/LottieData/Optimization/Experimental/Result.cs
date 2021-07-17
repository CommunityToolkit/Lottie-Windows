using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
#if PUBLIC_LottieData
    public
#endif

    struct Result<T>
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

        public Result(T? merged, double score = 0.0)
        {
            Value = merged;
            Score = score;
        }

        public static Result<T> Failed => new Result<T>(null);

        public static Result<T> From<TOther>(Result<TOther> other)
            where TOther : class, T
        {
            return new Result<T>(other.Value, other.Score);
        }
    }
}
