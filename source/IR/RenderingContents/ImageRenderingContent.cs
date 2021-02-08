// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class ImageRenderingContent : RenderingContent
    {
        internal ImageRenderingContent(ImageAsset imageAsset)
            => Image = imageAsset;

        public override bool IsAnimated => false;

        public override RenderingContent WithScale(Vector2 scale) => throw new System.NotImplementedException();

        public override RenderingContent WithTimeOffset(double timeOffset) => this;

        public ImageAsset Image { get; }
    }
}