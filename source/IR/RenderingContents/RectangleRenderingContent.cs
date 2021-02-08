// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class RectangleRenderingContent : RenderingContent
    {
        internal RectangleRenderingContent(
            IAnimatableVector2 size,
            Animatable<double> roundness)
        {
            Size = size;
            Roundness = roundness;
        }

        public Animatable<double> Roundness { get; }

        public IAnimatableVector2 Size { get; }

        public override bool IsAnimated => Roundness.IsAnimated || Size.IsAnimated;

        public override RenderingContent WithScale(Vector2 scale) => throw new System.NotImplementedException();

        public override RenderingContent WithTimeOffset(double timeOffset)
             => IsAnimated
                ? new RectangleRenderingContent(Size.WithTimeOffset(timeOffset), Roundness.WithTimeOffset(timeOffset))
                : this;

        public override string ToString() => $"Rectangle";
    }
}