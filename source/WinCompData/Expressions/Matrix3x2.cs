// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Matrix3x2 : Expression
    {
        readonly string _representation;

        Matrix3x2(string representation)
        {
            _representation = representation;
        }

        public static Matrix3x2 Zero { get; } = new Matrix3x2("Matrix3x2(0,0,0,0,0,0)");

        public static Matrix3x2 Identity { get; } = new Matrix3x2("Matrix3x2(1,0,0,1,0,0)");

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => _representation;

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Matrix3x2);
    }
}
