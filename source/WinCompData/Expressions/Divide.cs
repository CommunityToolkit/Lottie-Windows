// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Divide : BinaryExpression
    {
        public Divide(Expression left, Expression right)
            : base(left, right)
        {
        }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var a = Left.Simplified;
            var b = Right.Simplified;

            if (IsZero(a))
            {
                return a;
            }

            if (IsZero(b))
            {
                return b;
            }

            if (IsOne(a))
            {
                return b;
            }

            if (IsOne(b))
            {
                return a;
            }

            var numberA = a as Number;
            var numberB = b as Number;
            if (numberA != null && numberB != null)
            {
                // They're both constants. Evaluate them.
                return new Number(numberA.Value / numberB.Value);
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
        {
            var a = Left.Simplified;
            var b = Right.Simplified;

            var aString = Parenthesize(a);
            var bString = Parenthesize(b);

            return $"{aString} / {bString}";
        }

        /// <inheritdoc/>
        public override ExpressionType InferredType
        {
            get
            {
                var leftType = Left.InferredType;
                var rightType = Right.InferredType;

                return ExpressionType.AssertMatchingTypes(
                        TypeConstraint.Scalar | TypeConstraint.Vector2 | TypeConstraint.Vector3 | TypeConstraint.Vector4,
                        leftType,
                        rightType,
                        ExpressionType.IntersectConstraints(leftType.Constraints, rightType.Constraints));
            }
        }
    }
}
