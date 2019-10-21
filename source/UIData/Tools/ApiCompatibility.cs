// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Analyzes a graph to determine the features of the runtime required to instantiate it.
    /// </summary>
#if PUBLIC_UIData
    public
#endif
    sealed class ApiCompatibility
    {
        ApiCompatibility(uint requiredUapVersion)
        {
            RequiredUapVersion = requiredUapVersion;
        }

        public uint RequiredUapVersion { get; }

        /// <summary>
        /// Analyzes the given tree and returns information about its compatibility with a runtime.
        /// </summary>
        /// <returns>An object with properties describing the compatibility requirements of the tree.</returns>
        public static ApiCompatibility Analyze(CompositionObject graphRoot)
        {
            var objectGraph = ObjectGraph<Graph.Node>.FromCompositionObject(graphRoot, includeVertices: false);

            // Default to 7 (1809 10.0.17763.0) because that is the version in which Shapes became usable enough for Lottie.
            var requiredVersion = 7u;

            foreach (var node in objectGraph.CompositionObjectNodes)
            {
                switch (node.Object.Type)
                {
                    case CompositionObjectType.CompositionRadialGradientBrush:
                    case CompositionObjectType.CompositionVisualSurface:
                        requiredVersion = 8;
                        break;
                }
            }

            return new ApiCompatibility(requiredVersion);
        }
    }
}
