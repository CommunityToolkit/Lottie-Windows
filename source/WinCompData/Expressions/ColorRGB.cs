// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// Constructs a color from RGBA values.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    sealed class ColorRGB : Expression
    {
        public ColorRGB(Expression r, Expression g, Expression b, Expression a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public Expression R { get; }

        public Expression G { get; }

        public Expression B { get; }

        public Expression A { get; }

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"ColorRGB({A.Simplified},{R.Simplified},{G.Simplified},{B.Simplified})";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Color);
    }
}
