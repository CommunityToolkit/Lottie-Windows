// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Describes a configuration of the <see cref="LottieToWinCompTranslator"/>.
    /// </summary>
#if PUBLIC_LottieToWinComp
    public
#endif
    struct TranslatorConfiguration
    {
#pragma warning disable 0649
        /// <summary>
        /// Add descriptions that can be used by code generators to make code more readable.
        /// </summary>
        public bool AddCodegenDescriptions;

        /// <summary>
        /// Make the colors used by fills and strokes bindable so that they can be altered at runtime.
        /// </summary>
        public bool GenerateColorBindings;

        /// <summary>
        /// If true, throw an exception if translation issues are found.
        /// </summary>
        public bool StrictTranslation;

        /// <summary>
        /// The version of UAP for which the translator will ensure code compatibility. This
        /// value determines the minimum required SDK version required to build the generated
        /// code. Must be &gt;= 7.
        /// </summary>
        public uint TargetUapVersion;

        /// <summary>
        /// Translate the special property binding language in Lottie object
        /// names and create bindings to <see cref="WinCompData.CompositionPropertySet"/> values.
        /// </summary>
        public bool TranslatePropertyBindings;
#pragma warning restore 0649
    }
}
