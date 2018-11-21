// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class SolidColorStroke : ShapeLayerContent
    {
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
            DashPattern = dashPattern;
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

        public IEnumerable<double> DashPattern { get; }

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
