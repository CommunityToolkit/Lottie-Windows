// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Holds information about code being generated for an IAnimatedVisual.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    interface IAnimatedVisualInfo
    {
        /// <summary>
        /// Gets the name of the IAnimatedVisual class that will be exposed to users.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// The UAP version required by the IAnimatedVisual.
        /// </summary>
        uint RequiredUapVersion { get; }

        /// <summary>
        /// Do we need to implement CreateAnimations and DestroyAnimations method.
        /// Available after WinUI 2.8 with new interface IAnimatedVisual2.
        /// </summary>
        bool ImplementCreateAndDestroyMethods { get; }

        /// <summary>
        /// Gets the XAML LoadedImageSurface nodes of the AnimatedVisual.
        /// </summary>
        IReadOnlyList<LoadedImageSurfaceInfo> LoadedImageSurfaceNodes { get; }
    }
}
