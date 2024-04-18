using System.Numerics;
using Microsoft.UI.Composition;
using LottieIsland = CommunityToolkit.WinAppSDK.LottieIsland;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace LottieWinRT
{
    public sealed class AnimatedVisual : LottieIsland.IAnimatedVisual
    {
        private MUXC.IAnimatedVisual? _animatedVisual;

        public AnimatedVisual()
        {
        }

        internal AnimatedVisual(MUXC.IAnimatedVisual visual)
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
