// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if !WINDOWS_UWP
    public
#endif
    sealed class UntypedExpression : Expression
    {
        readonly string _value;

        public UntypedExpression(string value)
        {
            _value = value;
        }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => _value;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.AllValidTypes);
    }
}
