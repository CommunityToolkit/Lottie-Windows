// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Rectangle : Shape
    {
        public Rectangle(
            in ShapeLayerContentArgs args,
            DrawingDirection drawingDirection,
            IAnimatableVector3 position,
            IAnimatableVector3 size,
            Animatable<double> cornerRadius)
            : base(in args, drawingDirection)
        {
            Position = position;
            Size = size;
            CornerRadius = cornerRadius;
        }

        public Animatable<double> CornerRadius { get; }

        public IAnimatableVector3 Size { get; }

        public IAnimatableVector3 Position { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Rectangle;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.Rectangle;
    }
}
