// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Composition;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    sealed class DisposableAnimatedVisual : IAnimatedVisual, IDisposable
    {
        public Visual RootVisual { get; set; }

        public TimeSpan Duration { get; set; }

        public Vector2 Size { get; set; }

        public void Dispose()
        {
            RootVisual?.Dispose();
        }
    }
}
