// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public override string ToString() => $"Solid {Color}";
    }
}