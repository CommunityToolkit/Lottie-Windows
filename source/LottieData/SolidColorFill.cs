// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class SolidColorFill : ShapeLayerContent
    {
        public SolidColorFill(
            in ShapeLayerContentArgs args,
            PathFillType fillType,
            Animatable<Color> color,
            Animatable<double> opacityPercent)
            : base(in args)
        {
            FillType = fillType;
            Color = color;
            OpacityPercent = opacityPercent;
        }

        public Animatable<Color> Color { get; }

        public Animatable<double> OpacityPercent { get; }

        public PathFillType FillType { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.SolidColorFill;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.SolidColorFill;

        public enum PathFillType
        {
            EvenOdd,
            InverseWinding,
            Winding,
        }
    }
}
