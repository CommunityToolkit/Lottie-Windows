// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    static class IRToTreelessTransformer
    {
        public static TreelessComposition Transform(IRComposition source)
            => new TreelessComposition(source, Detreeify(source.Assets, source.Layers).ToArray());

        static IEnumerable<(IReadOnlyList<RenderingContext>, TreelessLayer)> Detreeify(AssetCollection assets, LayerCollection layers)
        {
            // TODO: this needs to return each layer and a chain of transforms for the layer made up
            //       of the transform stuff on each layer linked to a copy of the transform stuff
            //       from the tranform parents. The transform chain needs to be able to support
            //       everything that is current in the treeful layer, plus a clip and a visibility
            //      animation.
            foreach (var layer in layers.GetLayersBottomToTop())
            {
                switch (layer.Type)
                {
                    case Layer.LayerType.Null:
                        // Null layers only exist to hold transforms.
                        continue;

                    case Layer.LayerType.PreComp:
                        foreach (var contextAndLayer in DetreeifyPreCompLayer(assets, (PreCompLayer)layer))
                        {
                            yield return contextAndLayer;
                        }

                        break;

                    case Layer.LayerType.Image:
                        yield return DetreeifyImageLayer(assets, (ImageLayer)layer);

                    case Layer.LayerType.Shape:
                    case Layer.LayerType.Solid:
                    case Layer.LayerType.Text:

                    default:
                        throw Unreachable;
                }
            }
        }

        static IReadOnlyList<RenderingContext> GetRenderingContextsForLayer(Layer layer)
        {
            var visibiltyContext = new VisibilityRenderingContext { InPoint = layer.InPoint, OutPoint = layer.OutPoint };
            return new[] { visibiltyContext };
        }

        static (IReadOnlyList<RenderingContext>, TreelessLayer) DetreeifyImageLayer(AssetCollection assets, ImageLayer imageLayer)
        {
            return (GetRenderingContextsForLayer(imageLayer), new TreelessImageLayer());
        }

        static IEnumerable<(IReadOnlyList<RenderingContext>, TreelessLayer)> DetreeifyPreCompLayer(AssetCollection assets, PreCompLayer precompLayer)
        {
            var asset = assets.GetAssetById(precompLayer.RefId) as LayerCollectionAsset;
            if (asset is null)
            {
                // TODO - report an issue.
                yield break;
            }

            foreach (var (renderingContexts, childLayer) in Detreeify(assets, asset.Layers))
            {
                var precompRenderingContext = GetRenderingContextsForLayer(precompLayer);
                var combinedRenderingContexts = precompRenderingContext.Concat(renderingContexts).ToArray();
                yield return (combinedRenderingContexts, childLayer);
            }
        }

        static Exception Unreachable => new InvalidOperationException();
    }
}
