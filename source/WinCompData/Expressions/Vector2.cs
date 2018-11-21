// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Vector2 : Expression
    {
        public Expression X { get; }

        public Expression Y { get; }

        internal Vector2(Expression x, Expression y)
        {
            X = x;
            Y = y;
        }

        public static Vector2 operator *(Vector2 left, double right) => new Vector2(Multiply(left.X, Scalar(right)), Multiply(left.Y, Scalar(right)));

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"Vector2({Parenthesize(X)},{Parenthesize(Y)})";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector2);
    }
}
