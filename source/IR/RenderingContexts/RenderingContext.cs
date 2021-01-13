// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// The context in which a <see cref="RenderingContents.RenderingContent"/> is rendered.
    /// </summary>
    abstract class RenderingContext
    {
        internal static RenderingContext Compose(params RenderingContext[] renderingContexts)
            => new CompositeRenderingContext(renderingContexts);

        public static RenderingContext operator +(RenderingContext a, RenderingContext b)
            => Compose(a, b);

        /// <summary>
        /// Returns a context with all items except any of <typeparamref name="T"/> that
        /// return false for the predicate.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="RenderingContext"/> to filter.</typeparam>
        /// <returns>A filtered <see cref="RenderingContext"/>.</returns>
        public RenderingContext Filter<T>(Func<T, bool> predicate)
            => this is CompositeRenderingContext composite
                ? composite.Filter(predicate)
                : this;

        public static IEnumerable<RenderingContext> Filter<T>(IEnumerable<RenderingContext> items, Func<T, bool> predicate)
            => items.Where(item => !(item is T itemT) || predicate(itemT));
    }
}