// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using CommunityToolkit.WinAppSDK.LottieIsland;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace CommunityToolkit.WinUI.Lottie.Controls
{
    /// <summary>
    /// Simple wrapper to convert an <see cref="IAnimatedVisualFrameworkless"/> to an
    /// <see cref="IAnimatedVisual"/> for a Lottie composition. This allows
    /// a Lottie to be specified as the source for a <see cref="AnimatedVisualPlayer"/>.
    /// </summary>
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
