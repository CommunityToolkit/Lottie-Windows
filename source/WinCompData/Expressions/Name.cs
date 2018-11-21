// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// A name in an <see cref="Expression"/>.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class Name : Expression
    {
        internal static readonly Name[] EmptyNames = new Name[0];

        public Name(string value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        protected override Expression Simplify()
        {
            return this;
        }

        public string Value { get; private set; }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => Value;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.AllValidTypes);

        internal override bool IsAtomic => true;
    }
}
