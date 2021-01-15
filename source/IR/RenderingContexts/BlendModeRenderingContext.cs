// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class BlendModeRenderingContext : RenderingContext
    {
        internal BlendModeRenderingContext(BlendMode blendMode) => BlendMode = blendMode;

        public BlendMode BlendMode { get; }

        public override bool IsAnimated => false;

        public static RenderingContext WithoutRedundants(RenderingContext context)
             => context is CompositeRenderingContext composite
                        ? Compose(WithoutRedundants(composite.Items))
                        : context;

        static IEnumerable<RenderingContext> WithoutRedundants(IEnumerable<RenderingContext> items)
        {
            // Remove all but the last BlendMode and put it at the end of the list.
            BlendModeRenderingContext? lastBlendMode = null;

            foreach (var item in items)
            {
                if (item is BlendModeRenderingContext blendMode)
                {
                    lastBlendMode = blendMode;
                }
                else
                {
                    yield return item;
                }
            }

            if (lastBlendMode != null && lastBlendMode.BlendMode != BlendMode.Normal)
            {
                yield return lastBlendMode;
            }
        }

        public override RenderingContext WithTimeOffset(double timeOffset) => this;

        public override string ToString() => $"BlendMode {BlendMode}";
    }
}