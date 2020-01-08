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
        protected override Expression Simplify()
        {
            var channelNameLower = ChannelName?.ToLowerInvariant() ?? string.Empty;

            switch (Value)
            {
                case Vector4 vector:
                    switch (channelNameLower)
                    {
                        case "x":
                            return vector.X;
                        case "y":
                            return vector.Y;
                        case "z":
                            return vector.Z;
                        case "w":
                            return vector.W;
                    }

                    break;

                case Vector3 vector:
                    switch (channelNameLower)
                    {
                        case "x":
                            return vector.X;
                        case "y":
                            return vector.Y;
                        case "z":
                            return vector.Z;
                    }

                    break;

                case Vector2 vector:
                    switch (channelNameLower)
                    {
                        case "x":
                            return vector.X;
                        case "y":
                            return vector.Y;
                    }

                    break;
            }

            return this;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionString() => $"{Value}.{ChannelName}";

        /// <inheritdoc/>
        internal override bool IsAtomic => true;

        /// <inheritdoc/>
        public override ExpressionType InferredType => new ExpressionType(TypeConstraint.Scalar);
    }
}
