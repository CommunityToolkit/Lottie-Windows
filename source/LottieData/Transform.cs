// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    class Transform : ShapeLayerContent
    {
        public Transform(
            in ShapeLayerContentArgs args,
            IAnimatableVector2 anchor,
            IAnimatableVector2 position,
            IAnimatableVector2 scalePercent,
            Animatable<Rotation> rotation,
            Animatable<Opacity> opacity)
            : base(in args)
        {
            Anchor = anchor;
            Position = position;
            ScalePercent = scalePercent;
            Rotation = rotation;
            Opacity = opacity;
        }

        /// <summary>
        /// Gets the point around which scaling and rotation is performed, and from which the position is offset.
        /// </summary>
        public IAnimatableVector2 Anchor { get; }

        /// <summary>
        /// Gets the position, specified as the offset from the <see cref="Anchor"/>.
        /// </summary>
        public IAnimatableVector2 Position { get; }

        public IAnimatableVector2 ScalePercent { get; }

        public Animatable<Rotation> Rotation { get; }

        public Animatable<Opacity> Opacity { get; }

        public bool IsAnimated => Anchor.IsAnimated || Position.IsAnimated || ScalePercent.IsAnimated || Rotation.IsAnimated || Opacity.IsAnimated;

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Transform;
    }
}
