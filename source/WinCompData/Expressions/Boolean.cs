// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// A literal boolean, i.e. "true" or "false".
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class Boolean : Expression
    {
        public bool Value { get; }

        public Boolean(bool value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => Value ? "true" : "false";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Boolean);
    }
}
