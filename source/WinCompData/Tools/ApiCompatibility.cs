// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// define POST_RS5_SDK if using an SDK that is for a release
// after RS5
#if POST_RS5_SDK
// For allowing of Windows.UI.Composition.VisualSurface and the
// Lottie features that rely on it
#define AllowVisualSurface
#endif

using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools
{
    /// <summary>
    /// Analyzes a tree to determine the features of the runtime required to instantiate it.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    sealed class ApiCompatibility
    {
        ApiCompatibility(bool requiresCompositionGeometricClip, bool requiresCompositionVisualSurface)
        {
            RequiresCompositionGeometricClip = requiresCompositionGeometricClip;
            RequiresCompositionVisualSurface = requiresCompositionVisualSurface;
        }

        /// <summary>
        /// Analyzes the given tree and returns information about its compatibility with a runtime.
        /// </summary>
        /// <returns>An object with properties describing the compatibility requirements of the tree.</returns>
        public static ApiCompatibility Analyze(CompositionObject graphRoot)
        {
            // Always require CompostionGeometryClip - this ensures that we are never compatible with
            // RS4 (geometries are flaky in RS4, and CompositionGeometryClip is new in RS5).
            return new ApiCompatibility(
                                        requiresCompositionGeometricClip: true,
#if AllowVisualSurface
                                        requiresCompositionVisualSurface : true
#else
                                        requiresCompositionVisualSurface : false
#endif
            );
        }

        public bool RequiresCompositionGeometricClip { get; }

        public bool RequiresCompositionVisualSurface { get; }
    }
}
