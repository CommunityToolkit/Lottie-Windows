// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(7)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class BooleanKeyFrameAnimation : KeyFrameAnimation<bool, Expressions.Boolean>
    {
        internal BooleanKeyFrameAnimation()
            : base(null)
        {
        }

        BooleanKeyFrameAnimation(BooleanKeyFrameAnimation other)
            : base(other)
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.BooleanKeyFrameAnimation;

        internal override CompositionAnimation Clone() => new BooleanKeyFrameAnimation(this);
    }
}
