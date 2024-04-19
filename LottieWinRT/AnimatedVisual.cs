// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using LottieIsland = CommunityToolkit.WinAppSDK.LottieIsland;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace LottieWinRT
{
    public sealed class AnimatedVisual : LottieIsland.IAnimatedVisualFrameworkless
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

        public Visual? RootVisual
        {
            get
            {
                //Compositor? compositor = _animatedVisual?.RootVisual.Compositor;
                //SpriteVisual? v = compositor?.CreateSpriteVisual();
                //if (v != null)
                //{
                //    v.Size = new Vector2(200, 200);
                //    v.Brush = compositor?.CreateColorBrush(Colors.Blue);
                //}
                //return v;
                return _animatedVisual?.RootVisual;
            }
        }

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
