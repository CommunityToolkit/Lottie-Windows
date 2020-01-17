// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    class Transform : ShapeLayerContent
    {
        public Transform(
            in ShapeLayerContentArgs args,
            IAnimatableVector3 anchor,
            IAnimatableVector3 position,
            IAnimatableVector3 scalePercent,
            Animatable<double> rotationDegrees,
            Animatable<Opacity> opacity)
            : base(in args)
        {
            Anchor = anchor;
            Position = position;
            ScalePercent = scalePercent;
            RotationDegrees = rotationDegrees;
            Opacity = opacity;
        }

        /// <summary>
        /// Gets the point around which scaling and rotation is performed, and from which the position is offset.
        /// </summary>
        public IAnimatableVector3 Anchor { get; }

        /// <summary>
        /// Gets the position, specified as the offset from the <see cref="Anchor"/>.
        /// </summary>
        public IAnimatableVector3 Position { get; }

        public IAnimatableVector3 ScalePercent { get; }

        public Animatable<double> RotationDegrees { get; }

        public Animatable<Opacity> Opacity { get; }

        public bool IsAnimated => Anchor.IsAnimated || Position.IsAnimated || ScalePercent.IsAnimated || RotationDegrees.IsAnimated || Opacity.IsAnimated;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Transform;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Transform;
    }
}
