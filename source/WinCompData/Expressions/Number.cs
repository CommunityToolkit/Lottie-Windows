// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// A literal number.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    sealed class Number : Expression
    {
        public double Value { get; }

        public Number(double value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => ToString(Value);

        internal override bool IsAtomic => Value >= 0;

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        static string ToString(double value)
        {
            // Do not use "G9" here - Composition expressions do not understand
            // scientific notation (e.g. 1.2E06)
            var fValue = (float)value;
            return Math.Floor(fValue) == fValue
                ? fValue.ToString("0", CultureInfo.InvariantCulture)
                : fValue.ToString("0.0####################", CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Scalar);
    }
}
