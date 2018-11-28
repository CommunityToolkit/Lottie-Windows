// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Vector3 : Expression
    {
        public Expression X { get; }

        public Expression Y { get; }

        public Expression Z { get; }

        internal Vector3(Expression x, Expression y, Expression z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"Vector3({Parenthesize(X)},{Parenthesize(Y)},{Parenthesize(Z)})";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector3);
    }
}
