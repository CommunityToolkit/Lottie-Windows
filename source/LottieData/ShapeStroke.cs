// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class ShapeStroke : ShapeLayerContent
    {
        public ShapeStroke(
            in ShapeLayerContentArgs args,
            Animatable<double> opacityPercent)
            : base(in args)
        {
            OpacityPercent = opacityPercent;
        }

        public Animatable<double> OpacityPercent { get; }

        public abstract ShapeStrokeKind StrokeKind { get; }

        public enum ShapeStrokeKind
        {
            SolidColor,
            LinearGradient,
            RadialGradient,
        }
    }
}
