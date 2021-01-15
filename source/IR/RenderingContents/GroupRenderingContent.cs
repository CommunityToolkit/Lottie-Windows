// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    sealed class GroupRenderingContent : RenderingContent
    {
        internal GroupRenderingContent(IReadOnlyList<Rendering> items)
            => Items = items;

        public IReadOnlyList<Rendering> Items { get; }

        public override bool IsAnimated => Items.Any(item => item.IsAnimated);

        public override RenderingContent WithTimeOffset(double timeOffset)
            => IsAnimated
                ? new GroupRenderingContent(Items.Select(item => Rendering.UnifyTimebase(item, timeOffset)).ToArray())
                : this;

        public override string ToString() => $"{(IsAnimated ? "Animated" : "Static")} Group[{Items.Count}]";
    }
}