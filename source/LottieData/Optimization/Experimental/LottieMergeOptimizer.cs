// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// This optimizer is detecting similar parts of Lottie composition and if they are similar enough
    /// it will merge them together (while preserving the look of the composition).
    /// This optimizer works best in case of many duplicate or similar PreComp layers on top level.
    /// For example if you have an animated icon with many states that are represented by separate precomps.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    sealed class LottieMergeOptimizer
    {
        public static LottieComposition Optimize(LottieComposition composition)
        {
            List<Layer> layers = composition.Layers.GetLayersBottomToTop().ToList();

            // Sort layers by beginning of their visible time range.
            layers.Sort((a, b) => a.InPoint.CompareTo(b.InPoint));

            // Create merge helper instance. It can merge layers and will store all generated assets that are refrenced by new layers.
            var mergeHelper = new MergeHelper(composition);

            // Maximal distance between two animations (in frames).
            double maximalAllowedDistance = composition.OutPoint - composition.InPoint;

            // This optimizations works with the following heuristic:
            // 1. Let's fix minimal allowed score and allowed distance (distance between end and start point of two layers)
            // 2. Merge all layers that fit these constraints.
            // 3. Increase allowedDistance in inner loop
            // 4. Decrease minimalScore in outer loop
            // This heuristic is needed because in theory any two layers can be merged, but some of them can be merged with higher score.
            // And if we merge pairs with worse score first - it can result in worse optimization overall.
            // So we are trying to merge pairs of layers with the higher score first, while trying to merge pairs that are nerby
            // because if we merge two layers that are far away then we can't insert any other layer between them (for merging).
            // Example: we have three layers [0; 10] [30; 40] [70; 80]
            // It is better to merge [0; 10] and [30; 40] first (result is [0; 40]), and then merge [70; 80] with the result.
            // If we merge [0; 10] and [70; 80] first, we will get [0; 80] as the result, and we will not be able to merge it with [30; 40]
            // because they intersect.
            // TODO: there is surely a better heuristic, we need more experiments to find it.
            for (double minimalScore = 0.95; minimalScore > 0.1; minimalScore *= 0.8)
            {
                for (double allowedDistance = 1.0; allowedDistance < maximalAllowedDistance; allowedDistance *= 2.0)
                {
                    for (int i = 0; i < layers.Count; i++)
                    {
                        for (int j = i + 1; j < layers.Count; j++)
                        {
                            if (layers[j] is not PreCompLayer || layers[i] is not PreCompLayer)
                            {
                                continue;
                            }

                            if (layers[j].InPoint - layers[i].OutPoint > allowedDistance)
                            {
                                continue;
                            }

                            var mergeRes = mergeHelper.MergeLayers(layers[i], layers[j]);

                            // Score of merging two layers right now calculated as "intersection over minimum" of number of
                            // layers in layer collections. We can merge only PreCompLayers with LayerCollectionAsset's right now.
                            // If we have layer collection with 10 layers, and layer collection with 12 layers and after merge we get
                            // layer collection with 16 layers, then the score will be (10 + 12 - 16) / 10 = 0.6.
                            if (mergeRes.Success && minimalScore <= mergeRes.Score)
                            {
                                layers.RemoveAt(j);
                                layers.RemoveAt(i);

                                layers.Insert(i, mergeRes.Value!);
                                j--;
                            }
                        }
                    }
                }
            }

            List<Asset> usedAssets = new List<Asset>();

            // Add all generated and old assets, next optimizer will delte all unused assets.
            // TODO: detect and add only used assets. This is done to simplify the PR for now
            usedAssets.AddRange(mergeHelper.AssetsGenerated);
            usedAssets.AddRange(composition.Assets);

            return new LottieComposition(
                composition.Name,
                composition.Width,
                composition.Height,
                composition.InPoint,
                composition.OutPoint,
                composition.FramesPerSecond,
                composition.Is3d,
                composition.Version,
                new AssetCollection(usedAssets),
                composition.Chars,
                composition.Fonts,
                new LayerCollection(layers),
                composition.Markers,
                composition.ExtraData);
        }
    }
}