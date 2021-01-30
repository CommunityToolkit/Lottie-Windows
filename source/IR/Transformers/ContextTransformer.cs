// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    /// <summary>
    /// A method that transforms a <see cref="RenderingContext"/>.
    /// </summary>
    /// <returns>A possibly transformed version of input.</returns>
    delegate RenderingContext ContextTransformer(RenderingContext input);
}