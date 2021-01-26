// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    /// <summary>
    /// A <see cref="RenderingContent"/> that contains other <see cref="Rendering"/>s.
    /// This is used to express trees of <see cref="Rendering"/>s.
    /// </summary>
    sealed class ContainerRenderingContent : RenderingContent
    {
        internal ContainerRenderingContent(IReadOnlyList<Rendering> items)
            => Items = items;

        /// <summary>
        /// The <see cref="Rendering"/>s in the container, in drawing order.
        /// </summary>
        public IReadOnlyList<Rendering> Items { get; }

        public override bool IsAnimated => Items.Any(item => item.IsAnimated);

        public override RenderingContent WithTimeOffset(double timeOffset)
            => IsAnimated
                ? new ContainerRenderingContent(Items.Select(item => Rendering.UnifyTimebaseWithTimeOffset(item, timeOffset)).ToArray())
                : this;

        public override string ToString() => $"{(IsAnimated ? "Animated" : "Static")} Group[{Items.Count}]";
    }
}