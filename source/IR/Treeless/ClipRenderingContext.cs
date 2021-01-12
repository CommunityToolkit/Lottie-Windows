// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
    sealed class ClipRenderingContext : RenderingContext
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public override string ToString() => $"Clip: {Width}x{Height}";
    }
}
