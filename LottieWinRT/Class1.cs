// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Composition;
using System.Numerics;

namespace LottieWinRT
{
    public sealed class Class1 : CommunityToolkit.WinAppSDK.LottieIsland.IAnimatedVisual
    {
        public Class1()
        {
        }

        public TimeSpan Duration { get => TimeSpan.Zero; }

        public Visual? RootVisual { get => null; }

        public Vector2 Size { get => Vector2.Zero; }
    }
}
