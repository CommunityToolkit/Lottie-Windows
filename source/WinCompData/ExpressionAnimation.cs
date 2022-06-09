// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class ExpressionAnimation : CompositionAnimation
    {
        internal ExpressionAnimation(Expression expression)
            : this(null, expression)
        {
        }

        ExpressionAnimation(ExpressionAnimation? other, Expression expression)
            : base(other)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ExpressionAnimation;

        internal override CompositionAnimation Clone() => new ExpressionAnimation(this, Expression);

        /// <inheritdoc/>
        public override string ToString() => Expression.ToString();
    }
}
