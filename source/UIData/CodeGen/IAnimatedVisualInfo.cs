// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Holds information about code being generated for an IAnimatedVisual.
    /// </summary>
#if PUBLIC_UIData
    public
#endif
    interface IAnimatedVisualInfo
    {
        /// <summary>
        /// The <see cref="IAnimatedVisualSourceInfo"/> describing the IAnimatedVisualSource that
        /// will source the IAnimatedVisual described by this object.
        /// </summary>
        IAnimatedVisualSourceInfo AnimatedVisualSourceInfo { get; }

        /// <summary>
        /// Gets the name of the IAnimatedVisual class that will be exposed to users.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// Gets a value indicating whether the AnimatedVisual has LoadedImageSurface.
        /// </summary>
        bool HasLoadedImageSurface { get; }

        /// <summary>
        /// The UAP version required by the IAnimatedVisual.
        /// </summary>
        uint RequiredUapVersion { get; }

        /// <summary>
        /// Gets the XAML LoadedImageSurface nodes of the AnimatedVisual.
        /// </summary>
        IReadOnlyList<LoadedImageSurfaceInfo> LoadedImageSurfaceNodes { get; }
    }
}
