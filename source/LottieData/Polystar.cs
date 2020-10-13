// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Polystar : Shape
    {
        public Polystar(
            in ShapeLayerContentArgs args,
            DrawingDirection drawingDirection,
            PolyStarType starType,
            Animatable<double> points,
            IAnimatableVector3 position,
            Animatable<double> rotation,
            Animatable<double>? innerRadius,
            Animatable<double> outerRadius,
            Animatable<double>? innerRoundness,
            Animatable<double> outerRoundness)
            : base(in args, drawingDirection)
        {
            StarType = starType;
            Points = points;
            Position = position;
            Rotation = rotation;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            InnerRoundness = innerRoundness;
            OuterRoundness = outerRoundness;
        }

        internal PolyStarType StarType { get; }

        internal Animatable<double> Points { get; }

        internal IAnimatableVector3 Position { get; }

        internal Animatable<double> Rotation { get; }

        internal Animatable<double>? InnerRadius { get; }

        internal Animatable<double> OuterRadius { get; }

        internal Animatable<double>? InnerRoundness { get; }

        internal Animatable<double> OuterRoundness { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Polystar;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Polystar;

        public enum PolyStarType
        {
            Star,
            Polygon,
        }
    }
}
