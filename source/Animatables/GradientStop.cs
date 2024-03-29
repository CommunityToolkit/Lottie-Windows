// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.Animatables
{
#if PUBLIC_Animatables
    public
#endif
    abstract class GradientStop
    {
        private protected GradientStop(double offset)
        {
            Offset = offset;
        }

        public double Offset { get; }

        public abstract GradientStopKind Kind { get; }

        public enum GradientStopKind
        {
            Color,
            Opacity,
        }
    }
}
