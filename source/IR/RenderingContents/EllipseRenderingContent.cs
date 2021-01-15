// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class EllipseRenderingContent : RenderingContent
    {
        internal EllipseRenderingContent(IAnimatableVector3 diameter)
        {
            Diameter = diameter;
        }

        public IAnimatableVector3 Diameter { get; }

        public override bool IsAnimated => Diameter.IsAnimated;

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