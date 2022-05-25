// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CommunityToolkit.WinUI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Wrapper for Lottie <see cref="LottieData.Layer"/> class.
    /// It is mostly needed to keep pair of layer and matte layer together
    /// (layers with <see cref="Layer.MatteType.Invert"/> or <see cref="Layer.MatteType.Add"/>)
    /// because their order is fixed and matte layer should always go right before the main layer.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    class LayerGroup
    {
        /// <summary>
        /// Main layer of the group.
        /// </summary>
        public Layer MainLayer { get; }

        /// <summary>
        /// Matte layer. It is null for most groups.
        /// But if it is not null then it should be placed before main layer in layer collection.
        /// </summary>
        public Layer? MatteLayer { get; }

        /// <summary>
        /// Indicates if layer group can be merged with other layer groups.
        /// </summary>
        public bool CanBeMerged { get; }

        public LayerGroup(Layer mainLayer, bool canBeMerged)
        {
            MainLayer = mainLayer;
            MatteLayer = null;
            CanBeMerged = canBeMerged;
        }

        public LayerGroup(Layer mainLayer, Layer matteLayer, bool canBeMerged)
        {
            MainLayer = mainLayer;
            MatteLayer = matteLayer;
            CanBeMerged = canBeMerged;
        }

        public static List<LayerGroup> LayersToLayerGroups(List<Layer> layers, Func<Layer, Layer?, bool> canBeMergedFunc)
        {
            var layerGroups = new List<LayerGroup>();
            for (int i = 0; i < layers.Count; i++)
            {
                if (i + 1 < layers.Count && layers[i + 1].LayerMatteType != Layer.MatteType.None)
                {
                    var matteLayer = layers[i];
                    var mainLayer = layers[i + 1];
                    layerGroups.Add(new LayerGroup(mainLayer, matteLayer, canBeMergedFunc(mainLayer, matteLayer)));
                    i++;
                }
                else
                {
                    var mainLayer = layers[i];
                    layerGroups.Add(new LayerGroup(mainLayer, canBeMergedFunc(mainLayer, null)));
                }
            }

            return layerGroups;
        }

        public static List<Layer> LayerGroupsToLayers(List<LayerGroup> layerGroups)
        {
            List<Layer> layers = new List<Layer>();

            foreach (var layerGroup in layerGroups)
            {
                if (layerGroup.MatteLayer is not null)
                {
                    layers.Add(layerGroup.MatteLayer);
                }

                layers.Add(layerGroup.MainLayer);
            }

            // Ensure correct order of indices.
            for (int i = 0; i + 1 < layers.Count; i++)
            {
                Debug.Assert(layers[i].Index < layers[i + 1].Index, "Indexes must be sorted");
            }

            return layers;
        }
    }
}
