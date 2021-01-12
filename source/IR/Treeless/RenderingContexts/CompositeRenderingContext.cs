// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContexts
{
    sealed class CompositeRenderingContext : RenderingContext
    {
        internal CompositeRenderingContext(IEnumerable<RenderingContext> items)
        {
            // Flatten any Composite RenderingContexts so that Composite objects
            // are only ever at the top level.
            Items = items.SelectMany(rc => rc is CompositeRenderingContext crc
                ? crc.Items
                : new[] { rc }).ToArray();
        }

        public IReadOnlyList<RenderingContext> Items { get; }
    }
}
