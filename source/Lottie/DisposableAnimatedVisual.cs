// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml.Controls;

#if WINAPPSDK
using Microsoft.UI.Composition;
#else
using Windows.UI.Composition;
#endif

namespace CommunityToolkit.WinUI.Lottie
{
    sealed class DisposableAnimatedVisual : IAnimatedVisual, IDisposable
    {
        internal DisposableAnimatedVisual(Visual rootVisual, IEnumerable<AnimationController> customAnimationControllers)
        {
            RootVisual = rootVisual;
            CustomAnimationControllers = customAnimationControllers;
        }

        public Visual RootVisual { get; }

        /// <summary>
        /// Keeps references to all custom AnimationController objects.
        /// We need references because otherwise they will be destroyed from dwm.
        /// </summary>
        public IEnumerable<AnimationController> CustomAnimationControllers { get; }

        public TimeSpan Duration { get; set; }

        public Vector2 Size { get; set; }

        public void Dispose()
        {
            RootVisual?.Dispose();
        }
    }
}
