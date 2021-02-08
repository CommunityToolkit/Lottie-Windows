// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    abstract class RenderingContent
    {
        private protected RenderingContent()
        {
        }

        public abstract bool IsAnimated { get; }

        public static RenderingContent Null { get; } = new NullRenderingContent();

        public abstract RenderingContent WithScale(Vector2 scale);

        public abstract RenderingContent WithTimeOffset(double timeOffset);

        /// <summary>
        /// Renders nothing.
        /// </summary>
        sealed class NullRenderingContent : RenderingContent
        {
            public override string ToString() => "Null";

            public override bool IsAnimated => false;

            public override RenderingContent WithScale(Vector2 scale) => this;

            public override RenderingContent WithTimeOffset(double timeOffset) => this;
        }
    }
}