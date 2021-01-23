// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

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
            Animatable<double> roundness)
            : base(in args, drawingDirection)
        {
            Position = position;
            Size = size;
            Roundness = roundness;
        }

        /// <summary>
        /// Determines how round the corners of the rectangle are. If the rectangle
        /// is a square and the roundness is equal to half of the width then the
        /// rectangle will be rendered as a circle. Once the roundness value reaches
        /// half of the minimum of the shortest dimension, increasing it has no
        /// further effect.
        /// </summary>
        public Animatable<double> Roundness { get; }

        public IAnimatableVector3 Size { get; }

        public IAnimatableVector3 Position { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Rectangle;

        public override ShapeType ShapeType => ShapeType.Rectangle;
    }
}
