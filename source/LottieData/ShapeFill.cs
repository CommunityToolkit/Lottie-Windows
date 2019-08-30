// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class ShapeFill : ShapeLayerContent
    {
        public ShapeFill(
            in ShapeLayerContentArgs args,
            PathFillType fillType,
            Animatable<double> opacityPercent)
            : base(in args)
        {
            OpacityPercent = opacityPercent;
            FillType = fillType;
        }

        public Animatable<double> OpacityPercent { get; }

        public abstract ShapeFillKind FillKind { get; }

        public PathFillType FillType { get; }

        public enum ShapeFillKind
        {
            SolidColor,
            LinearGradient,
            RadialGradient,
        }

        public enum PathFillType
        {
            EvenOdd,
            InverseWinding,
            Winding,
        }
    }
}
