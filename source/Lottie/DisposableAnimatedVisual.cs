// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using Microsoft.UI.Xaml.Controls;

#if Lottie_Windows_WinUI3
using Microsoft.UI.Composition;
#else
using Windows.UI.Composition;
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    sealed class DisposableAnimatedVisual : IAnimatedVisual, IDisposable
    {
        internal DisposableAnimatedVisual(Visual rootVisual)
        {
            RootVisual = rootVisual;
        }

        public Visual RootVisual { get; }

        public TimeSpan Duration { get; set; }

        public Vector2 Size { get; set; }

        public void Dispose()
        {
            RootVisual?.Dispose();
        }
    }
}
