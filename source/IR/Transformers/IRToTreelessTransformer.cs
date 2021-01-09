// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    static class IRToTreelessTransformer
    {
        public static TreelessComposition Transform(IRComposition source)
            => new TreelessComposition(source, Detreeify(source.Layers)).ToArray();

        static IEnumerable<TreelessLayer> Detreeify(AssetCollection assets, LayerCollection layers)
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
                        foreach (var childLayer in DetreeifyPreCompLayer(assets, (PreCompLayer)layer))
                        {
                            yield return childLayer;
                        }

                        break;

                        // TODO - convert to the equivalent treeless layer.
                    case Layer.LayerType.Image:
                    case Layer.LayerType.Shape:
                    case Layer.LayerType.Solid:
                    case Layer.LayerType.Text:

                    default:
                        throw Unreachable;
                }
            }
        }

        static IEnumerable<TreelessLayer> DetreeifyPreCompLayer(AssetCollection assets, PreCompLayer precompLayer)
        {
            var asset = assets.GetAssetById(precompLayer.RefId) as LayerCollectionAsset;
            if (asset is null)
            {
                // TODO - report an issue.
                yield break;
            }

            foreach (var childLayer in Detreeify(assets, asset.Layers))
            {
                // TODO: this needs to return each layer layer and a chain of transforms including
                //       a copy of the transform stuff from this precomp (including a clip and
                //       visiblity animation).
                yield return childLayer;
            }
        }

        static Exception Unreachable => new InvalidOperationException();
    }
}
