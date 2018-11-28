// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// Raises a value to the power of 2.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class Squared : Expression
    {
        public Squared(Expression value)
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
                ? new Number(numberValue.Value * numberValue.Value)
                : (Expression)this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"Square({Value.Simplified})";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType
        {
            get
            {
                return new ExpressionType(TypeConstraint.AllValidTypes);
            }
        }
    }
}
