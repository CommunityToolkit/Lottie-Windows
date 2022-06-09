// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
    /// <summary>
    /// A collection of <see cref="Layer"/>s in drawing order.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class LayerCollection
    {
        readonly Layer[] _layers;

        public LayerCollection(IEnumerable<Layer> layers)
        {
            // The Index value is supposed to determine the drawing order (highest number
            // is drawn first) and should be unique within a LayerCollection, however I've
            // seen cases where 2 layers have the same index.
            // In order to be resilient to non-unique indices, sort by index using a stable
            // sort so that the layers with non-unique indices remain in the order they
            // were specified in (this assumes that the specified order is actually
            // the drawing order).
            // If a request is made to find a layer with a non-unique index, return null.
            // OrderBy is a stable sort, so non-unique keys will remain in their original order.
            _layers = layers.OrderByDescending(layer => layer.Index).ToArray();
        }

        public static LayerCollection Empty { get; } = new LayerCollection(Array.Empty<Layer>());

        /// <summary>
        /// Returns the <see cref="Layer"/>s in the <see cref="LayerCollection"/> in
        /// painting order.
        /// </summary>
        /// <returns>The <see cref="Layer"/>s in painting order.</returns>
        public IEnumerable<Layer> GetLayersBottomToTop() => _layers;

        /// <summary>
        /// Returns the <see cref="Layer"/> with the given id, or null if no matching <see cref="Layer"/> is found.
        /// </summary>
        /// <returns>The corresponding <see cref="Layer"/> or null if <paramref name="id"/> does not match
        /// a single <see cref="Layer"/> in the collection.</returns>
        public Layer? GetLayerById(int? id)
        {
            if (!id.HasValue)
            {
                return null;
            }

            return BinarySearch(id.Value);
        }

        Layer? BinarySearch(int targetIndex)
        {
            int min = 0;
            int max = _layers.Length - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                ref var current = ref _layers[mid];
                var currentIndex = current.Index;
                if (targetIndex == currentIndex)
                {
                    // Check that the index is unique by looking at the indices
                    // on either side. If the index is not unique, return null.
                    if ((mid > 0 && _layers[mid - 1].Index == targetIndex) ||
                        (mid < _layers.Length - 1 && _layers[mid + 1].Index == targetIndex))
                    {
                        // Index is not unique. We don't know which Layer is needed, so return null.
                        return null;
                    }
                    else
                    {
                        return current;
                    }
                }
                else if (targetIndex > currentIndex)
                {
                    // Look left.
                    max = mid - 1;
                }
                else
                {
                    // Look right.
                    min = mid + 1;
                }
            }

            // Not found.
            return null;
        }

        public LayerCollection WithTimeOffset(double offset)
        {
            return new LayerCollection(_layers.Select(layer => layer.WithTimeOffset(offset)));
        }
    }
}
