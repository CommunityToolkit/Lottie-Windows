// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// The result of translating a Lottie animation into an equivalent WinCompData form.
    /// </summary>
#if PUBLIC
    public
#endif
    sealed class TranslationResult
    {
        internal TranslationResult(
            Visual rootVisual,
            IEnumerable<(string code, string description)> translationIssues,
            uint minimumRequiredUapVersion)
        {
            RootVisual = rootVisual;
            TranslationIssues = translationIssues.ToArray();
            MinimumRequiredUapVersion = minimumRequiredUapVersion;
        }

        /// <summary>
        /// The <see cref="Visual"/> at the root of the translation, or null if the translation failed.
        /// </summary>
        public Visual RootVisual { get; }

        /// <summary>
        /// The list of issues discovered during translation.
        /// </summary>
        public IReadOnlyList<(string code, string description)> TranslationIssues { get; }

        /// <summary>
        /// The minimum version of UAP required to instantiate the result of the translation.
        /// </summary>
        public uint MinimumRequiredUapVersion { get; }
    }
}
