// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    // PathKeyFrameAnimation was added in 6, but there were bugs that made
    // it unreliable until 11.
    [MetaData.UapVersion(11)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class PathKeyFrameAnimation
        : KeyFrameAnimation<CompositionPath, Expressions.Void>
    {
        internal PathKeyFrameAnimation()
            : base(null)
        {
        }

        PathKeyFrameAnimation(PathKeyFrameAnimation other)
            : base(other)
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.PathKeyFrameAnimation;

        internal override CompositionAnimation Clone() => new PathKeyFrameAnimation(this);
    }
}
