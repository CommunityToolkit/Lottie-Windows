// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class ColorKeyFrameAnimation : KeyFrameAnimation<Wui.Color>
    {
        internal ColorKeyFrameAnimation()
            : base(null)
        {
        }

        ColorKeyFrameAnimation(ColorKeyFrameAnimation other)
            : base(other)
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.ColorKeyFrameAnimation;

        internal override CompositionAnimation Clone() => new ColorKeyFrameAnimation(this);
    }
}
