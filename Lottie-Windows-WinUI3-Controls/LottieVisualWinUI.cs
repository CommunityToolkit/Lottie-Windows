using System;
using System.Numerics;
using CommunityToolkit.WinAppSDK.LottieIsland;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace CommunityToolkit.WinUI.Lottie.Controls
{
    internal class LottieVisualWinUI : IAnimatedVisual
    {
        IAnimatedVisualFrameworkless _animatedVisual;

        internal LottieVisualWinUI(IAnimatedVisualFrameworkless animatedVisual)
        {
            _animatedVisual = animatedVisual;
        }

        public TimeSpan Duration => _animatedVisual.Duration;

        public Visual RootVisual => _animatedVisual.RootVisual;

        public Vector2 Size => _animatedVisual.Size;

        public void Dispose()
        {
            _animatedVisual.As<IDisposable>().Dispose();
        }
    }
}
