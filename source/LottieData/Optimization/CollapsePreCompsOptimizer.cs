// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// This optimizer is trying to optimize the most common Lottie scenario - usage for AnimatedIcon.
    /// AnimatedIcons have many animation segments for different states of the icon and in most
    /// cases they are represented by non-intersecting <see cref="PreCompLayer"/>s. Often these layers
    /// are referencing the same RefId in Asset collection so it means that in fact we can use
    /// only one <see cref="PreCompLayer"/> entry to display an animation for two (or more) identical segments.
    ///
    /// This optimizer checks if this is a scenario described above, and if it is, it performs collapsing
    /// of <see cref="PreCompLayer"/>s that have same RefId into one.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    sealed class CollapsePreCompsOptimizer
    {
        public static LottieComposition Optimize(LottieComposition composition)
        {
            // Example of how this optimization works:
            //
            // m - markers
            //
            // Before optimization:
            //
            // |__ RefId: comp_0 __| |________ RefId: comp_1 ________| |__ RefId : comp_0 __| |__ RefId : comp_2 __|
            // ^                   ^ ^                               ^ ^                    ^ ^                    ^
            // m0                 m1 m2                             m3 m4                  m5 m6                  m7
            //
            //
            // Step 1 (delete duplicates)
            //
            // |__ RefId: comp_0 __| |________ RefId: comp_1 ________|                        |__ RefId : comp_2 __|
            // ^                   ^ ^                               ^ ^                    ^ ^                    ^
            // m0                 m1 m2                             m3 m4                  m5 m6                  m7
            //
            //
            // Step 2 (shift all layers to form one contiguous section)
            //
            // |__ RefId: comp_0 __| |________ RefId: comp_1 ________| |__ RefId : comp_2 __|
            // ^                   ^ ^                               ^ ^                    ^ ^                    ^
            // m0                 m1 m2                             m3 m4                  m5 m6                  m7
            //
            //
            // Step 3 (final, move all markers to corresponding layers):
            //
            // |__ RefId: comp_0 __| |________ RefId: comp_1 ________| |__ RefId : comp_2 __|
            // ^                   ^ ^                               ^ ^                    ^
            // m0                 m1 m2                             m3 m6                  m7
            // m4                 m5
            //
            // We deleted second entry of comp_0, shifted comp_2 to the left and moved markers m4 and m5 to the same spots where m0 and m1 are.
            List<Layer> layers = composition.Layers.GetLayersBottomToTop().ToList();

            // All layers should be pre-comp layers.
            if (!layers.All(a => a is PreCompLayer) || layers.Count == 0)
            {
                return composition;
            }

            // Sort layers by beginning of their time range.
            layers.Sort((a, b) => a.InPoint.CompareTo(b.InPoint));

            // There should not be intersecting layers.
            for (int i = 1; i < layers.Count; i++)
            {
                if (layers[i - 1].OutPoint > layers[i].InPoint)
                {
                    return composition;
                }
            }

            // AnimatedIcon uses pair of markers to represent animation segment.
            var startMarkers = new Dictionary<string, Marker>();
            var endMarkers = new Dictionary<string, Marker>();

            foreach (var marker in composition.Markers)
            {
                if (marker.Name.EndsWith("_End"))
                {
                    // End markers have %s_End format.
                    endMarkers.Add(marker.Name.Substring(0, marker.Name.Length - "_End".Length), marker);
                }
                else if (marker.Name.EndsWith("_Start"))
                {
                    // Start markers have %s_Start format.
                    startMarkers.Add(marker.Name.Substring(0, marker.Name.Length - "_Start".Length), marker);
                }
                else
                {
                    // All markers should have %s_Start or %s_End format.
                    return composition;
                }
            }

            // Each Start should match to one End.
            if (startMarkers.Count != endMarkers.Count)
            {
                return composition;
            }

            // Next part of this function will perform PreComps collapsing.
            // We are iterating over layers in order and checking if we can find
            // another layer that has been added to the result and referencing the same RefId.
            //
            // After all layers are collapsed we can end up with some gaps where there is no animations/layers.
            // We can shift all the layers so that there will be no gaps, for this we need layerInPointOffset.
            var layerInPointOffset = new Dictionary<int, double>();
            var layersAfterCollapse = new List<Layer>();

            for (int i = 0; i < layers.Count; i++)
            {
                // Find layer that is referencing the same RefId in already processed layers.
                int previousSameLayer = layersAfterCollapse.FindIndex(layer => ((PreCompLayer)layer).RefId == ((PreCompLayer)layers[i]).RefId);

                if (previousSameLayer == -1)
                {
                    // If there were no processed layers, we will offset time of the first layer to start at 0.
                    if (layersAfterCollapse.Count == 0)
                    {
                        layerInPointOffset[i] = -layers[i].InPoint;
                    }
                    else
                    {
                        // Otherwise we will offset new layer to start right after previous processed layer.
                        layerInPointOffset[i] = layersAfterCollapse[layersAfterCollapse.Count - 1].OutPoint - layers[i].InPoint;
                    }

                    layersAfterCollapse.Add(layers[i].WithTimeOffset(layerInPointOffset[i]));
                }
                else
                {
                    // If we found a layer that is referencing the same RefId we should offset new layer to start at the same point.
                    // But we do not need to add this to the layersAfterCompression, since the same layer is already there.
                    layerInPointOffset[i] = layersAfterCollapse[previousSameLayer].InPoint - layers[i].InPoint;
                }
            }

            // Next part of this function will offset markers to match new layer positions.
            var markersAfterOffset = new List<Marker>();

            foreach (var key in startMarkers.Keys)
            {
                // For each start there should be an end.
                if (!endMarkers.ContainsKey(key))
                {
                    return composition;
                }

                // Each pair of start and end should correspond to some PreCompLayer and should be inside of its time segment.
                int correspondingLayer = layers.FindIndex(
                    layer => layer.InPoint <= startMarkers[key].Frame &&
                        startMarkers[key].Frame <= endMarkers[key].Frame &&
                        endMarkers[key].Frame <= layer.OutPoint);

                if (correspondingLayer == -1)
                {
                    return composition;
                }

                // Offset each marker with the same shift as corresponding layer was shifted.
                markersAfterOffset.Add(startMarkers[key].WithTimeOffset(layerInPointOffset[correspondingLayer]));
                markersAfterOffset.Add(endMarkers[key].WithTimeOffset(layerInPointOffset[correspondingLayer]));
            }

            return new LottieComposition(
                composition.Name,
                composition.Width,
                composition.Height,
                layersAfterCollapse[0].InPoint,
                layersAfterCollapse[layersAfterCollapse.Count - 1].OutPoint,
                composition.FramesPerSecond,
                composition.Is3d,
                composition.Version,
                composition.Assets,
                composition.Chars,
                composition.Fonts,
                new LayerCollection(layersAfterCollapse),
                markersAfterOffset,
                composition.ExtraData);
        }
    }
}