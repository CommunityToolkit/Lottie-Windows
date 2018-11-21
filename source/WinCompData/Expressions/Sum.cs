// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Sum : BinaryExpression
    {
        public Sum(Expression left, Expression right)
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
                return b;
            }

            if (IsZero(b))
            {
                return a;
            }

            if (a is Number numberA && b is Number numberB)
            {
                return Sum(numberA, numberB);
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
        {
            var a = Left.Simplified;
            var b = Right.Simplified;

            var aString = a is Sum ? a.ToString() : Parenthesize(a);
            var bString = b is Sum ? b.ToString() : Parenthesize(b);

            return $"{aString} + {bString}";
        }

        /// <inheritdoc/>
        public override ExpressionType InferredType =>
            ExpressionType.ConstrainToTypes(TypeConstraint.Scalar, Left.InferredType, Right.InferredType);
    }
}
