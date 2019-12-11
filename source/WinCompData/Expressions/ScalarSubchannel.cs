// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class ScalarSubchannel : Expression
    {
        internal ScalarSubchannel(Expression value, string channelName)
        {
            Value = value;
            ChannelName = channelName;
        }

        public Expression Value { get; }

        public string ChannelName { get; }

        /// <inheritdoc/>
        protected override Expression Simplify() => this;

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"{Value}.{ChannelName}";

        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Scalar);
    }
}
