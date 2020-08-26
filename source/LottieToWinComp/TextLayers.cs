// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    static class TextLayers
    {
        public static LayerTranslator CreateTextLayerTranslator(TextLayerContext context)
        {
            // Text layers are not yet suported.
            context.Issues.TextLayerIsNotSupported();
            return null;
        }
    }
}
