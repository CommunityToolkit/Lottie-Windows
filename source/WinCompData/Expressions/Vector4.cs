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
        readonly Expression _x;
        readonly Expression _y;
        readonly Expression _z;
        readonly Expression _w;

        internal Vector4(Expression x, Expression y, Expression z, Expression w)
        {
            _x = x;
            _y = y;
            _z = z;
            _w = w;
        }

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        protected override string CreateExpressionString()
            => $"Vector4({Parenthesize(_x)},{Parenthesize(_y)},{Parenthesize(_z)},{Parenthesize(_w)})";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Vector4);
    }
}
