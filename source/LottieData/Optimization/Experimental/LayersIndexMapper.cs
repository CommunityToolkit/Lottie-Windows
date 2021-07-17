using System.Collections.Generic;

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

        public void RemapLayer(Layer layer)
        {
            // TODO: create layer copy instad of assigning new index(!)
            layer.Index = GetMapping(layer.Index);
            if (layer.Parent is not null)
            {
                // TODO: same as above(!)
                layer.Parent = GetMapping((int)layer.Parent);
            }
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
