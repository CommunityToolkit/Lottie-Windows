﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.LottieData;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    sealed class SolidLayerContext : LayerContext
    {
        internal SolidLayerContext(CompositionContext compositionContext, SolidLayer layer)
            : base(compositionContext, layer)
        {
            Layer = layer;
        }

        public new SolidLayer Layer { get; }
    }
}