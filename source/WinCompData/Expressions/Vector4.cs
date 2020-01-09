// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Vector4 : Expression
    {
        internal Vector4(Expression x, Expression y, Expression z, Expression w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public new Expression X { get; }

        public new Expression Y { get; }

        public new Expression Z { get; }

        public new Expression W { get; }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var x = X.Simplified;
            var y = Y.Simplified;
            var z = Z.Simplified;
            var w = W.Simplified;

            return x == X && y == Y && z == Z && w == W
                ? this
                : new Vector4(x, y, z, w);
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"Vector4({Parenthesize(X)},{Parenthesize(Y)},{Parenthesize(Z)},{Parenthesize(W)})";

        internal static bool IsZero(Vector4 value) => IsZero(value.X) && IsZero(value.Y) && IsZero(value.Z) && IsZero(value.W);

        internal static bool IsOne(Vector4 value) => IsOne(value.X) && IsOne(value.Y) && IsOne(value.Z) && IsOne(value.W);

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector4);
    }
}
