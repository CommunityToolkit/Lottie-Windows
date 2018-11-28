// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class SolidColorStroke : ShapeLayerContent
    {
        readonly double[] _dashPattern;

        public SolidColorStroke(
            string name,
            string matchName,
            Animatable<double> dashOffset,
            IEnumerable<double> dashPattern,
            Animatable<Color> color,
            Animatable<double> opacityPercent,
            Animatable<double> thickness,
            LineCapType capType,
            LineJoinType joinType,
            double miterLimit)
            : base(name, matchName)
        {
            DashOffset = dashOffset;
            _dashPattern = dashPattern.ToArray();
            Color = color;
            OpacityPercent = opacityPercent;
            Thickness = thickness;
            CapType = capType;
            JoinType = joinType;
            MiterLimit = miterLimit;
        }

        public Animatable<Color> Color { get; }

        public Animatable<double> OpacityPercent { get; }

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
