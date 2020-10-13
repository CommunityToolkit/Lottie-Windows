// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class Expression_<T> : Expression, IEquatable<T>
        where T : Expression_<T>
    {
        string? _expressionTextCache;
        T? _simplifiedExpressionCache;

        private protected Expression_()
        {
        }

        /// <summary>
        /// Gets a simplified form of the expression. May be the same as this.
        /// </summary>
        // Expressions are immutable, so it's always safe to return a cached version.
        public T Simplified => _simplifiedExpressionCache ??= Simplify();

        /// <inheritdoc/>
        // Expressions are immutable, so it's always safe to return a cached version.
        // Always return the simplified version.
        public override string ToText() => _expressionTextCache ??= Simplified.CreateExpressionText();

        protected virtual T Simplify() => (T)this;

        public bool Equals(T? other) => !(other is null) && other.ToText() == ToText();
    }
}