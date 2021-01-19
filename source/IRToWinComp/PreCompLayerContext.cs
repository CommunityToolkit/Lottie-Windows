// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.IR;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    sealed class PreCompLayerContext : LayerContext
    {
        internal PreCompLayerContext(CompositionContext compositionContext, PreCompLayer layer)
            : base(compositionContext, layer)
        {
            Layer = layer;

            var referencedLayers = GetLayerCollectionByAssetId(this, layer.RefId);

            // Precomps define a new temporal and spatial space for their child layers.
            ChildrenCompositionContext = new CompositionContext(
                compositionContext.Translation,
                compositionContext,
                layer.Name,
                referencedLayers,
                size: ConvertTo.Vector2(layer.Size),
                startTime: compositionContext.StartTime - layer.StartTime,
                durationInFrames: compositionContext.DurationInFrames);
        }

        public new PreCompLayer Layer { get; }

        public CompositionContext ChildrenCompositionContext { get; }

        static LayerCollection GetLayerCollectionByAssetId(PreCompLayerContext context, string assetId)
            => ((LayerCollectionAsset?)context.Translation.GetAssetById(context, assetId, Asset.AssetType.LayerCollection))?.Layers ??
                LayerCollection.Empty;
    }
}
