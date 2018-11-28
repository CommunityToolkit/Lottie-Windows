// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// Constraints its child <see cref="Expression"/> to a given set of types.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    sealed class TypeAssert : Expression
    {
        readonly Expression _child;
        readonly TypeConstraint _constraints;

        public TypeAssert(Expression child, TypeConstraint constraints)
        {
            _child = child;
            _constraints = constraints;
        }

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        // There is no syntax for a type assert, so just return the child syntax.
        /// <inheritdoc/>
        protected override string CreateExpressionString() => _child.ToString();

        /// <inheritdoc/>
        public override ExpressionType InferredType => ExpressionType.ConstrainToType(_constraints, _child.InferredType);

        internal override bool IsAtomic => _child.IsAtomic;
    }
}
