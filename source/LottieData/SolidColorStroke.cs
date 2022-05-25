// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.Animatables;

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class SolidColorStroke : ShapeStroke
    {
        readonly double[] _dashPattern;

        public SolidColorStroke(
            in ShapeLayerContentArgs args,
            Animatable<double> dashOffset,
            IEnumerable<double> dashPattern,
            Animatable<Color> color,
            Animatable<Opacity> opacity,
            Animatable<double> strokeWidth,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit)
            : base(in args, opacity, strokeWidth, capType, joinType, miterLimit)
        {
            DashOffset = dashOffset;
            _dashPattern = dashPattern.ToArray();
            Color = color;
        }

        public Animatable<Color> Color { get; }

        public IReadOnlyList<double> DashPattern => _dashPattern;

        public Animatable<double> DashOffset { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.SolidColorStroke;

        public override ShapeStrokeKind StrokeKind => ShapeStrokeKind.SolidColor;

        public override ShapeLayerContent WithTimeOffset(double offset)
        {
            return new SolidColorStroke(
                CopyArgs(),
                DashOffset.WithTimeOffset(offset),
                DashPattern,
                Color.WithTimeOffset(offset),
                Opacity.WithTimeOffset(offset),
                StrokeWidth.WithTimeOffset(offset),
                CapType,
                JoinType,
                MiterLimit
                );
        }
    }
}
