// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes
{
    abstract class Brush
    {
        private protected Brush(Animatable<Opacity> opacity)
        {
            Opacity = opacity;
        }

        public abstract bool IsAnimated { get; }

        public abstract Brush WithTimeOffset(double timeOffset);

        public Animatable<Opacity> Opacity { get; }
    }
}