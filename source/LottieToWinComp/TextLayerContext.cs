﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    sealed class TextLayerContext : LayerContext
    {
        internal TextLayerContext(CompositionContext compositionContext, TextLayer layer)
            : base(compositionContext, layer)
        {
            Layer = layer;
        }

        public new TextLayer Layer { get; }
    }
}