// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Holds information about code being generated for an IAnimatedVisual.
    /// </summary>
    public sealed class AnimatedVisualInfo
    {
        internal AnimatedVisualInfo(
            AnimatedVisualSourceInfo animatedVisualSourceInfo,
            string className)
        {
            AnimatedVisualSourceInfo = animatedVisualSourceInfo;
            ClassName = className;
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
    }
}
