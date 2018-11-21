// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A collection of <see cref="Layer"/>s in drawing order.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class LayerCollection
    {
        readonly Dictionary<int, Layer> _layersById;

        public LayerCollection(IEnumerable<Layer> layers)
        {
            _layersById = layers.ToDictionary(layer => layer.Index);
        }

        /// <summary>
        /// Returns the <see cref="Layer"/>s in the <see cref="LayerCollection"/> in
        /// painting order.
        /// </summary>
        public IEnumerable<Layer> GetLayersBottomToTop() => _layersById.Values.OrderByDescending(layer => layer.Index);

        /// <summary>
        /// Returns the <see cref="Layer"/> with the given id, or null if no matching <see cref="Layer"/> is found.
        /// </summary>
        public Layer GetLayerById(int? id)
        {
            if (!id.HasValue)
            {
                return null;
            }

            return _layersById.TryGetValue(id.Value, out var result) ? result : null;
        }
    }
}
