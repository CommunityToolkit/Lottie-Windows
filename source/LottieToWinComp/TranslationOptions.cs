// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Describes options to use when translating a Lottie file.
    /// </summary>
    public struct TranslationOptions
    {
        /// <summary>
        /// Add descriptions that can be used by code generators to make code more readable.
        /// </summary>
        public bool AddCodegenDescriptions { get; set; }

        /// <summary>
        /// Make the colors used by fills and strokes bindable so that they can be altered at runtime.
        /// </summary>
        public bool GenerateColorBindings { get; set; }

        /// <summary>
        /// Translate the special property binding language in Lottie object
        /// names and create bindings to <see cref="WinCompData.CompositionPropertySet"/> values.
        /// </summary>
        public bool TranslatePropertyBindings { get; set; }
    }
}
