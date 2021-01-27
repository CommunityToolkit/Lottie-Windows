// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
#if PUBLIC_Animatables
    public
#endif
    sealed class OpacityGradientStop : GradientStop
    {
        public OpacityGradientStop(double offset, Opacity opacity)
            : base(offset)
        {
            Opacity = opacity;
        }

        public Opacity Opacity { get; }

        /// <inheritdoc/>
        public override GradientStopKind Kind => GradientStopKind.Opacity;

        /// <inheritdoc/>
        public override string ToString() => $"{Opacity.Percent}%@{Offset}";
    }
}
