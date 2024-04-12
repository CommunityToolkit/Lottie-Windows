using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;

namespace LottieWinRT
{
    public sealed class AnimatedVisualWinRT
    {
        private IAnimatedVisual? _animatedVisual;

        public AnimatedVisualWinRT()
        {
        }

        internal AnimatedVisualWinRT(IAnimatedVisual visual)
        {
            _animatedVisual = visual;
        }

        public TimeSpan Duration
        {
            get
            {
                if (_animatedVisual == null)
                {
                    return TimeSpan.Zero;
                }
                else
                {
                    return _animatedVisual.Duration;
                }
            }
        }

        public Visual? RootVisual { get => _animatedVisual?.RootVisual; }

        public Vector2 Size
        {
            get
            {
                if (_animatedVisual == null)
                {
                    return Vector2.Zero;
                }
                else
                {
                    return _animatedVisual.Size;
                }
            }
        }
    }
}
