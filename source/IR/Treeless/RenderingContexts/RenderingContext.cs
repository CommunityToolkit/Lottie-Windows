// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContexts
{
    /// <summary>
    /// The context in which a <see cref="RenderingContents.RenderingContent"/> is rendered.
    /// </summary>
    abstract class RenderingContext
    {
        internal static RenderingContext Compose(params RenderingContext[] renderingContexts)
            => new CompositeRenderingContext(renderingContexts);

        internal virtual RenderingContext Append(params RenderingContext[] renderingContexts)
            => new CompositeRenderingContext(renderingContexts.Prepend(this));

        public static RenderingContext operator +(RenderingContext a, RenderingContext b)
            => Compose(a, b);
    }
}
