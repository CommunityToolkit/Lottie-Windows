// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents
{
    /// <summary>
    /// Renders nothing.
    /// </summary>
    sealed class NullRenderingContent : RenderingContent
    {
        NullRenderingContent()
        {
        }

        public static NullRenderingContent Instance { get; } = new NullRenderingContent();

        public override string ToString() => "Null";
    }
}