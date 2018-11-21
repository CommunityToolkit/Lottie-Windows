// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if !WINDOWS_UWP
    public
#endif
    sealed class LessThan : BinaryExpression
    {
        public LessThan(Expression left, Expression right)
            : base(left, right)
        {
        }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var a = Left.Simplified;
            var b = Right.Simplified;

            var numberA = a as Number;
            var numberB = b as Number;
            if (numberA != null && numberB != null)
            {
                // They're both constants. Evaluate them.
                return new Boolean(numberA.Value < numberB.Value);
            }

            if (a != Left || b != Right)
            {
                return new LessThan(a, b);
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"{Parenthesize(Left.Simplified)} < {Parenthesize(Right.Simplified)}";

        /// <inheritdoc/>
        public override ExpressionType InferredType =>
            ExpressionType.AssertMatchingTypes(TypeConstraint.Scalar, Left.InferredType, Right.InferredType, TypeConstraint.Boolean);
    }
}
