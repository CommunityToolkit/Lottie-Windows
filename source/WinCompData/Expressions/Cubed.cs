// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// Raises a value to the power of 3.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    sealed class Cubed : Expression
    {
        public Cubed(Expression value)
        {
            Value = value;
        }

        public Expression Value { get; }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var simplifiedValue = Value.Simplified;
            var numberValue = simplifiedValue as Number;
            return (numberValue != null)
                ? new Number(numberValue.Value * numberValue.Value * numberValue.Value)
                : (Expression)this;
        }

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        protected override string CreateExpressionString()
        {
            var simplifiedValue = Value.Simplified;

            return $"Pow({simplifiedValue}, 3)";
        }

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Scalar);
    }
}
