// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class RoundCorners : ShapeLayerContent
    {
        public RoundCorners(
            in ShapeLayerContentArgs args,
            Animatable<double> radius)
            : base(in args)
        {
            Radius = radius;
        }

        /// <summary>
        /// The radius of the rounding.
        /// </summary>
        /// <remarks>
        /// If the shape to which this applies is a rectangle, the rounding will
        /// only apply if the rectangle has a 0 roundness value. Once the radius
        /// value reaches half of the largest dimension of the rectangle, the
        /// result will be equivalent to an ellipse of the same size, and
        /// increasing the radius further will have no further effect.
        /// </remarks>
        public Animatable<double> Radius { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.RoundCorners;
    }
}
