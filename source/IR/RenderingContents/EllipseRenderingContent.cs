// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class EllipseRenderingContent : RenderingContent
    {
        internal EllipseRenderingContent(IAnimatableVector2 diameter)
        {
            Diameter = diameter;
        }

        public IAnimatableVector2 Diameter { get; }

        public override bool IsAnimated => Diameter.IsAnimated;

        public override RenderingContent WithScale(Vector2 scale) => throw new System.NotImplementedException();

        public override RenderingContent WithTimeOffset(double timeOffset)
        {
            if (Diameter.IsAnimated)
            {
                return new EllipseRenderingContent(Diameter.WithTimeOffset(timeOffset));
            }
            else
            {
                return this;
            }
        }

        public override string ToString() => $"Ellipse";
    }
}