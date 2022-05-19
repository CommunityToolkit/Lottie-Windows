// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#if Lottie_Windows_WinUI3
namespace MicrosoftToolkit.WinUI.Lottie
#else
namespace Microsoft.Toolkit.Uwp.UI.Lottie
#endif
{
    /// <summary>
    /// Options for controlling how the <see cref="LottieVisualSource"/> processes a Lottie file.
    /// </summary>
    [Flags]
    public enum LottieVisualOptions
    {
        /// <summary>
        /// No options set.
        /// </summary>
        None = 0,

        /// <summary>
        /// Optimizes the translation of the Lottie so as to reduce resource
        /// usage during rendering. Note that this may slow down loading.
        /// </summary>
        Optimize = 1,

        /// <summary>
        /// Sets the AnimatedVisualPlayer.Diagnostics property with information
        /// about the Lottie.
        /// </summary>
        IncludeDiagnostics = 2,

        /// <summary>
        /// Bind each distinct color of the Lottie to a property set value so that
        /// the colors can be dynamically updated.
        /// </summary>
        BindableColors = 4,

        /// <summary>
        /// Enables all options.
        /// </summary>
        All = BindableColors | IncludeDiagnostics | Optimize,
    }
}
