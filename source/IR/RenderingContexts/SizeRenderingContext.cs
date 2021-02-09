// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// Defines the size of some content. The content is clipped to this size.
    /// </summary>
    sealed class SizeRenderingContext : RenderingContext
    {
        internal SizeRenderingContext(Vector2 size)
        {
            Size = size;
        }

        protected override sealed bool DependsOn(RenderingContext other)
        {
            switch (other)
            {
                case RotationRenderingContext _:
                case ScaleRenderingContext _:
                    return true;
            }

            return false;
        }

        public Vector2 Size { get; }

        public override bool IsAnimated => false;

        public override RenderingContext WithOffset(Vector2 offset) => this;

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public override string ToString() => $"Size: {Size.X}x{Size.Y}";
    }
}
