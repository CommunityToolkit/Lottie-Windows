// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Polystar : ShapeLayerContent
    {
        public Polystar(
            in ShapeLayerContentArgs args,
            bool direction,
            PolyStarType starType,
            Animatable<double> points,
            IAnimatableVector3 position,
            Animatable<double> rotation,
            Animatable<double> innerRadius,
            Animatable<double> outerRadius,
            Animatable<double> innerRoundedness,
            Animatable<double> outerRoundedness)
            : base(in args)
        {
            Direction = direction;
            StarType = starType;
            Points = points;
            Position = position;
            Rotation = rotation;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            InnerRoundedness = innerRoundedness;
            OuterRoundedness = outerRoundedness;
        }

        internal bool Direction { get; }

        internal PolyStarType StarType { get; }

        internal Animatable<double> Points { get; }

        internal IAnimatableVector3 Position { get; }

        internal Animatable<double> Rotation { get; }

        internal Animatable<double> InnerRadius { get; }

        internal Animatable<double> OuterRadius { get; }

        internal Animatable<double> InnerRoundedness { get; }

        internal Animatable<double> OuterRoundedness { get; }

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
