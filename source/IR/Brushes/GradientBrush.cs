// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes
{
    abstract class GradientBrush : Brush
    {
        private protected GradientBrush(
            IAnimatableVector2 startPoint,
            IAnimatableVector2 endPoint,
            Animatable<Sequence<GradientStop>> gradientStops,
            Animatable<Opacity> opacity)
            : base(opacity)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            GradientStops = gradientStops;
        }

        public IAnimatableVector2 StartPoint { get; }

        public IAnimatableVector2 EndPoint { get; }

        public Animatable<Sequence<GradientStop>> GradientStops { get; }

        public override bool IsAnimated =>
            StartPoint.IsAnimated ||
            EndPoint.IsAnimated ||
            GradientStops.IsAnimated ||
            Opacity.IsAnimated;
    }
}