using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Helper data structure that manages layers index remapping.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    class LayersIndexMapper
    {
        Dictionary<int, int> indexMapping = new Dictionary<int, int>();

        public void SetMapping(int oldIndex, int newIndex)
        {
            indexMapping[oldIndex] = newIndex;
        }

        public int GetMapping(int oldIndex)
        {
            return indexMapping[oldIndex];
        }

        public Layer RemapLayer(Layer layer)
        {
            return layer.CopyAndChangeIndices(GetMapping(layer.Index), layer.Parent is null ? null : GetMapping((int)layer.Parent));
        }

        public List<Layer> RemapLayers(List<Layer> layers)
        {
            return layers.Select(layer => RemapLayer(layer)).ToList();
        }

        public LayerGroup RemapLayerGroup(LayerGroup layerGroup)
        {
            if (layerGroup.MatteLayer is null)
            {
                return new LayerGroup(RemapLayer(layerGroup.MainLayer), layerGroup.CanBeMerged);
            }

            return new LayerGroup(RemapLayer(layerGroup.MainLayer), RemapLayer(layerGroup.MatteLayer), layerGroup.CanBeMerged);
        }

        public List<LayerGroup> RemapLayerGroups(List<LayerGroup> layerGroups)
        {
            return layerGroups.Select(lyerGroup => RemapLayerGroup(lyerGroup)).ToList();
        }

        public class IndexGenerator
        {
            int generator = 0;

            public int GenerateIndex()
            {
                return generator++;
            }
        }
    }
}
