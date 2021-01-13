// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    sealed class CompositeRenderingContext : RenderingContext
    {
        internal CompositeRenderingContext(IEnumerable<RenderingContext> items)
        {
            // Flatten any Composite RenderingContexts so that Composite objects
            // are only ever at the top level.
            var flattened = items.SelectMany(rc => rc is CompositeRenderingContext crc
                ? crc.Items
                : new[] { rc });

            // Filter out any NullRenderingContexts.
            Items = flattened.Where(context => !(context is NullRenderingContext)).ToArray();
        }

        public IReadOnlyList<RenderingContext> Items { get; }

        /// <summary>
        /// Returns a context with all items except any of <typeparamref name="T"/> that
        /// return false for the predicate.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="RenderingContext"/> to filter.</typeparam>
        /// <returns>A filtered <see cref="RenderingContext"/>.</returns>
        public new RenderingContext Filter<T>(Func<T, bool> predicate)
            => new CompositeRenderingContext(RenderingContext.Filter(Items, predicate));

        public override string ToString() => $"Composite {Items.Where(item => item is not MetadataRenderingContext).Count()}";
    }
}
