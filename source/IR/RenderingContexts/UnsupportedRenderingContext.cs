// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class UnsupportedRenderingContext : RenderingContext
    {
        internal UnsupportedRenderingContext(string description)
            => Description = description;

        public string Description { get; }

        public override bool IsAnimated => false;

        public override sealed RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public override string ToString() => Description;
    }
}
