// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Optimization
{
    static class RenderingContextOptimizer
    {
        internal static RenderingContext Optimize(RenderingContext input)
        {
            if (input is CompositeRenderingContext composite)
            {
                IEnumerable<RenderingContext> items = composite.Items;
                items = RemoveRedundantAnchors(items);
                items = RemoveRedundantBlendModes(items);
                items = RemoveRedundantOpacities(items);
                items = RemoveRedundantPositions(items);
                items = RemoveRedundantRotations(items);
                items = RemoveRedundantScales(items);
                return new CompositeRenderingContext(items);
            }
            else
            {
                return input;
            }
        }

        internal static IEnumerable<RenderingContext> RemoveRedundantAnchors(IEnumerable<RenderingContext> items)
            => RenderingContext.Filter(items, (AnchorRenderingContext context) =>
                        context.Anchor.IsAnimated || context.Anchor.InitialValue.X != 0 || context.Anchor.InitialValue.Y != 0);

        internal static IEnumerable<RenderingContext> RemoveRedundantOpacities(IEnumerable<RenderingContext> items)
            => RenderingContext.Filter(items, (OpacityRenderingContext context) =>
                        !context.Opacity.IsAlways(Opacity.Opaque));

        internal static IEnumerable<RenderingContext> RemoveRedundantPositions(IEnumerable<RenderingContext> items)
            => RenderingContext.Filter(items, (PositionRenderingContext context) =>
                        context.Position.IsAnimated || context.Position.InitialValue.X != 0 || context.Position.InitialValue.Y != 0);

        internal static IEnumerable<RenderingContext> RemoveRedundantRotations(IEnumerable<RenderingContext> items)
            => RenderingContext.Filter(items, (RotationRenderingContext context) =>
                        !context.Rotation.IsAlways(Rotation.None));

        internal static IEnumerable<RenderingContext> RemoveRedundantScales(IEnumerable<RenderingContext> items)
            => RenderingContext.Filter(items, (ScaleRenderingContext context) =>
                context.ScalePercent.IsAnimated || context.ScalePercent.InitialValue.X != 100 || context.ScalePercent.InitialValue.Y != 100);

        internal static IEnumerable<RenderingContext> RemoveRedundantBlendModes(IEnumerable<RenderingContext> items)
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
    }
}
