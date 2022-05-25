// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.WinCompData;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates a <see cref="LottieComposition"/> to an equivalent <see cref="Visual"/>.
    /// </summary>
    /// <remarks>
    /// See https://helpx.adobe.com/pdf/after_effects_reference.pdf"/> for the
    /// After Effects semantics.
    /// </remarks>
#if PUBLIC
    public
#endif
    static class LottieToWinCompTranslator
    {
        /// <summary>
        /// The lowest UAP version for which the translator can produce code. Code from the translator
        /// will never be compatible with UAP versions less than this.
        /// </summary>
        // 7 (1809 10.0.17763.0) because that is the version in which Shapes became usable enough for Lottie.
        public static uint MinimumTargetUapVersion => 7;

        /// <summary>
        /// Attempts to translates the given <see cref="LottieComposition"/>.
        /// </summary>
        /// <param name="lottieComposition">The <see cref="LottieComposition"/> to translate.</param>
        /// <param name="configuration">Controls the configuration of the translator.</param>
        /// <returns>The result of the translation.</returns>
        public static TranslationResult TryTranslateLottieComposition(
            LottieComposition lottieComposition,
            in TranslatorConfiguration configuration)
        {
            return TranslationContext.TryTranslateLottieComposition(lottieComposition, configuration);
        }
    }
}