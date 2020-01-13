// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class ScalarKeyFrameAnimation : KeyFrameAnimation<float, Expressions.Scalar>
    {
        internal ScalarKeyFrameAnimation()
            : base(null)
        {
        }

        ScalarKeyFrameAnimation(ScalarKeyFrameAnimation other)
            : base(other)
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ScalarKeyFrameAnimation;

        internal override CompositionAnimation Clone() => new ScalarKeyFrameAnimation(this);
    }
}
