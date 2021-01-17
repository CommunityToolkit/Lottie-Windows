// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class SolidRenderingContent : RenderingContent
    {
        internal SolidRenderingContent(
            int width,
            int height,
            Color color)
        {
            Width = width;
            Height = height;
            Color = color;
        }

        public Color Color { get; }

        public int Width { get; }

        public int Height { get; }

        public override bool IsAnimated => false;

        public override RenderingContent WithTimeOffset(double timeOffset) => this;
    }
}