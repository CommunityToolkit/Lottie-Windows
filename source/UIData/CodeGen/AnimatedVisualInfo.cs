// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using static Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.InstantiatorGeneratorBase;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Holds information about code being generated for an IAnimatedVisual.
    /// </summary>
    public sealed class AnimatedVisualInfo
    {
        internal AnimatedVisualInfo(
            AnimatedVisualSourceInfo animatedVisualSourceInfo,
            string className,
            IReadOnlyList<LoadedImageSurfaceInfo> loadedImageSurfaceNodes)
        {
            AnimatedVisualSourceInfo = animatedVisualSourceInfo;
            ClassName = className;
            LoadedImageSurfaceNodes = loadedImageSurfaceNodes;
        }

        /// <summary>
        /// The <see cref="AnimatedVisualSourceInfo"/> describing the IAnimatedVisualSource that
        /// will source the IAnimatedVisual described by this object.
        /// </summary>
        public AnimatedVisualSourceInfo AnimatedVisualSourceInfo { get; }

        /// <summary>
        /// Gets the name of the IAnimatedVisual class that will be exposed to users.
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// Gets a value indicating whether the AnimatedVisual has LoadedImageSurface.
        /// </summary>
        public bool HasLoadedImageSurface => LoadedImageSurfaceNodes.Count > 0;

        /// <summary>
        /// Gets the LoadedImageSurface nodes of the AnimatedVisual.
        /// </summary>
        internal IReadOnlyList<LoadedImageSurfaceInfo> LoadedImageSurfaceNodes { get; }
    }
}
