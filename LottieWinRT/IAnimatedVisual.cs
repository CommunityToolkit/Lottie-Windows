using System.Numerics;
using Microsoft.UI.Composition;

namespace LottieWinRT
{
    public interface IAnimatedVisual
    {
        public TimeSpan Duration { get; }

        public Visual? RootVisual { get; }

        public Vector2 Size { get; }
    }
}
