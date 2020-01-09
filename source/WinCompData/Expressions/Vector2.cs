// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Vector2 : Expression
    {
        internal Vector2(Expression x, Expression y)
        {
            X = x;
            Y = y;
        }

        public new Expression X { get; }

        public new Expression Y { get; }

        public static Vector2 operator *(Vector2 left, double right)
            => new Vector2(Multiply(left.X, Scalar(right)), Multiply(left.Y, Scalar(right)));

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var x = X.Simplified;
            var y = Y.Simplified;

            return x == X && y == Y
                ? this
                : new Vector2(x, y);
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"Vector2({Parenthesize(X)},{Parenthesize(Y)})";

        internal static bool IsZero(Vector2 value) => IsZero(value.X) && IsZero(value.Y);

        internal static bool IsOne(Vector2 value) => IsOne(value.X) && IsOne(value.Y);

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector2);
    }
}
