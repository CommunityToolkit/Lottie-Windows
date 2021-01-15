// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class CompositeRenderingContext : RenderingContext
    {
        internal CompositeRenderingContext(IReadOnlyList<RenderingContext> items)
        {
            Items = items;
        }

        public IReadOnlyList<RenderingContext> Items { get; }

        public override bool IsAnimated => Items.Any(item => item.IsAnimated);

        public override RenderingContext WithTimeOffset(double timeOffset)
             => IsAnimated
                ? new CompositeRenderingContext(Items.Select(item => item.WithTimeOffset(timeOffset)).ToArray())
                : this;

        public override string ToString()
            => $"{(IsAnimated ? "Animated" : "Static")} RenderingContext[{Items.Count}]";
    }
}
