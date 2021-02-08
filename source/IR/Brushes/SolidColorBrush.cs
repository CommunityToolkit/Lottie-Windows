// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes
{
    sealed class SolidColorBrush : Brush
    {
        public SolidColorBrush(
            Animatable<Color> color,
            Animatable<Opacity> opacity)
            : base(opacity)
            => Color = color;

        public Animatable<Color> Color { get; }

        public override bool IsAnimated => Color.IsAnimated || Opacity.IsAnimated;

        public override Brush WithOffset(Vector2 offset) => this;

        public override Brush WithScale(Vector2 scale) => this;

        public override Brush WithTimeOffset(double timeOffset)
            => IsAnimated
                ? new SolidColorBrush(Color.WithTimeOffset(timeOffset), Opacity.WithTimeOffset(timeOffset))
                : this;

        public override string ToString() => $"Solid {Color}";
    }
}