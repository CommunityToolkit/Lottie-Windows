// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class Vector3 : Expression
    {
        internal Vector3(Expression x, Expression y, Expression z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public new Expression X { get; }

        public new Expression Y { get; }

        public new Expression Z { get; }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            var x = X.Simplified;
            var y = Y.Simplified;
            var z = Z.Simplified;
            return x == X && y == Y && z == Z
                ? this
                : new Vector3(x, y, z);
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"Vector3({Parenthesize(X)},{Parenthesize(Y)},{Parenthesize(Z)})";

        internal static bool IsZero(Vector3 value) => IsZero(value.X) && IsZero(value.Y) && IsZero(value.Z);

        internal static bool IsOne(Vector3 value) => IsOne(value.X) && IsOne(value.Y) && IsOne(value.Z);

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector3);
    }
}
