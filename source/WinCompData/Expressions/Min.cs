// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Min : BinaryExpression
    {
        public Min(Expression left, Expression right)
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
                return new Number(Math.Min(numberA.Value, numberB.Value));
            }

            if (a != Left || b != Right)
            {
                return new Min(a, b);
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"Min({Parenthesize(Left.Simplified)}, {Parenthesize(Right.Simplified)})";

        /// <inheritdoc/>
        public override ExpressionType InferredType =>
            ExpressionType.AssertMatchingTypes(
                TypeConstraint.Scalar | TypeConstraint.Vector2 | TypeConstraint.Vector3 | TypeConstraint.Vector4,
                Left.InferredType, Right.InferredType,
                TypeConstraint.Scalar | TypeConstraint.Vector2 | TypeConstraint.Vector3 | TypeConstraint.Vector4);
    }
}
