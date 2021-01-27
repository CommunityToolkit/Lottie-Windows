// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Ellipse : Shape
    {
        public Ellipse(
            in ShapeLayerContentArgs args,
            DrawingDirection drawingDirection,
            IAnimatableVector3 position,
            IAnimatableVector3 diameter)
            : base(in args, drawingDirection)
        {
            Position = position;
            Diameter = diameter;
        }

        public IAnimatableVector3 Position { get; }

        public IAnimatableVector3 Diameter { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.Ellipse;

        public override ShapeType ShapeType => ShapeType.Ellipse;
    }
}
