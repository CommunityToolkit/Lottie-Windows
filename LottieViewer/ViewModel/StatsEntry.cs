using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LottieViewer.ViewModel
{
    public class StatsEntry
    {
        public const uint LOTTIE_COMPOSITION_TAG = 1 << 0;
        public const uint WINDOWS_COMPOSITION_TAG = 1 << 1;
        public const uint LAYER_TAG = 1 << 2;
        public const uint MASK_TAG = 1 << 3;
        public const uint EFFECT_TAG = 1 << 4;

        internal StatsEntry(string name, int count, uint tags)
        {
            Name = name;
            Count = count;
            Tags = tags;
        }

        public string Name { get; }

        public int Count { get; }

        public uint Tags { get; }
    }
}
