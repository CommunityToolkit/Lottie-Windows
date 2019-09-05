// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
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
            Animatable<double> opacityPercent,
            Animatable<double> thickness,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit)
            : base(in args, opacityPercent)
        {
            DashOffset = dashOffset;
            _dashPattern = dashPattern.ToArray();
            Color = color;
            Thickness = thickness;
            CapType = capType;
            JoinType = joinType;
            MiterLimit = miterLimit;
        }

        public Animatable<Color> Color { get; }

        public Animatable<double> Thickness { get; }

        public ReadOnlySpan<double> DashPattern => _dashPattern;

        public Animatable<double> DashOffset { get; }

        public LineCapType CapType { get; }

        public LineJoinType JoinType { get; }

        public double MiterLimit { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.SolidColorStroke;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.SolidColorStroke;

        public override ShapeStrokeKind StrokeKind => ShapeStrokeKind.SolidColor;

        public enum LineCapType
        {
            Butt,
            Round,
            Projected,
        }

        public enum LineJoinType
        {
            Miter,
            Round,
            Bevel,
        }
    }
}
