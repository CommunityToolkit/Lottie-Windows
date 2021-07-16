using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
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
            layer.Index = GetMapping(layer.Index);
            if (layer.Parent is not null)
            {
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
