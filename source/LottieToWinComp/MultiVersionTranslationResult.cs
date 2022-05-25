// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
#if PUBLIC
    public
#endif
    sealed class MultiVersionTranslationResult
    {
        internal MultiVersionTranslationResult(
            IEnumerable<TranslationResult> translationResults,
            IEnumerable<(TranslationIssue, UapVersionRange)> issues)
        {
            TranslationResults = translationResults.ToArray();
            Issues = issues.ToArray();
        }

        /// <summary>
        /// The <see cref="TranslationResult"/>s for the UAP version sub-ranges. These
        /// are ordered from latest version to oldest version.
        /// </summary>
        public IReadOnlyList<TranslationResult> TranslationResults { get; }

        /// <summary>
        /// The issues from the translation. These are the same issues reported in
        /// <see cref="TranslationResult"/> but organized by UAP version range.
        /// </summary>
        public IReadOnlyList<(TranslationIssue, UapVersionRange)> Issues { get; }
    }
}