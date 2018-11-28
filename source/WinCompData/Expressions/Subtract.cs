// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Subtract : BinaryExpression
    {
        internal Subtract(Expression left, Expression right)
            : base(left, right)
        {
        }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var a = Left.Simplified;
            var b = Right.Simplified;
            if (IsZero(b))
            {
                return a;
            }

            var numberA = a as Number;
            var numberB = b as Number;

            // If both are numbers, simplify to the calculated value.
            if (numberA != null && numberB != null)
            {
                return new Number(numberA.Value - numberB.Value);
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"{Parenthesize(Left.Simplified)} - {Parenthesize(Right.Simplified)}";

        /// <inheritdoc/>
        public override ExpressionType InferredType =>
            ExpressionType.ConstrainToTypes(
                TypeConstraint.Scalar | TypeConstraint.Vector2 | TypeConstraint.Vector3 | TypeConstraint.Vector4,
                Left.InferredType, Right.InferredType);
    }
}
