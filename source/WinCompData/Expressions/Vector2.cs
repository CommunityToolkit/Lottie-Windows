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
        readonly Expression _x;
        readonly Expression _y;

        internal Vector2(Expression x, Expression y)
        {
            _x = x;
            _y = y;
        }

        public static Vector2 operator *(Vector2 left, double right)
            => new Vector2(Multiply(left._x, Scalar(right)), Multiply(left._y, Scalar(right)));

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"Vector2({Parenthesize(_x)},{Parenthesize(_y)})";

        internal static bool IsZero(Vector2 value) => IsZero(value._x) && IsZero(value._y);

        internal static bool IsOne(Vector2 value) => IsOne(value._x) && IsOne(value._y);

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector2);
    }
}
