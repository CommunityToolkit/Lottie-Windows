// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Layer;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.ShapeLayerContent;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
#if PUBLIC_LottieData
    public
#endif

    sealed class LottieCompositionOptimizer
    {
        public static LottieComposition GetOptimized(LottieComposition composition)
        {
            List<Layer> layers = composition.Layers.GetLayersBottomToTop().ToList();

            layers.Sort((a, b) => a.InPoint.CompareTo(b.InPoint));

            var mergeHelper = new MergeHelper(composition);

            double maximalAllowedDistance = composition.OutPoint - composition.InPoint;

            for (double minimalScore = 0.95; minimalScore > 0.1; minimalScore *= 0.8)
            {
                for (double allowedDistance = 1.0; allowedDistance < maximalAllowedDistance; allowedDistance *= 2.0)
                {
                    for (int i = 0; i < layers.Count; i++)
                    {
                        for (int j = i + 1; j < layers.Count; j++)
                        {
                            if (layers[j].InPoint - layers[i].OutPoint > allowedDistance)
                            {
                                continue;
                            }

                            var mergeRes = mergeHelper.MergeLayers(layers[i], layers[j]);

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