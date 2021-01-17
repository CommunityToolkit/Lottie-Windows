// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
    public
#endif
    abstract class ShapeFill : ShapeLayerContent
    {
        public ShapeFill(
            in ShapeLayerContentArgs args,
            PathFillType fillType,
            Animatable<Opacity> opacity)
            : base(in args)
        {
            Opacity = opacity;
            FillType = fillType;
        }

        public Animatable<Opacity> Opacity { get; }

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
