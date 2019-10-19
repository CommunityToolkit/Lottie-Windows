// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
#if PUBLIC
    public
#endif
    sealed class LottieToMultiVersionWinCompTranslator
    {
        // We will never produce code that is compatible with code before version 7.
        const uint LowestValidUapVersion = 7;

        /// <summary>
        /// Attempts to translates the given <see cref="LottieData.LottieComposition"/> for a range of UAP versions,
        /// producing one or more translations.
        /// </summary>
        /// <param name="lottieComposition">The <see cref="LottieComposition"/> to translate.</param>
        /// <param name="targetUapVersion">The version of UAP that the translator will ensure compatibility with. Must be >= 7.</param>
        /// <param name="minimumUapVersion">The lowest version of UAP on which the result must run. Must be >= 7 and &lt;= targetUapVersion.</param>
        /// <param name="strictTranslation">If true, throw an exception if translation issues are found.</param>
        /// <param name="addCodegenDescriptions">Add descriptions to objects for comments on generated code.</param>
        /// <returns>The results of the translation and the issues.</returns>
        public static (IReadOnlyList<TranslationResult> translationResults, IReadOnlyList<(TranslationIssue, UapVersionRange)> issues) TryTranslateLottieComposition(
            LottieComposition lottieComposition,
            uint targetUapVersion,
            uint minimumUapVersion,
            bool strictTranslation,
            bool addCodegenDescriptions = true)
        {
            if (targetUapVersion < LowestValidUapVersion)
            {
                throw new ArgumentException(nameof(targetUapVersion));
            }

            if (minimumUapVersion > targetUapVersion || minimumUapVersion < LowestValidUapVersion)
            {
                throw new ArgumentException(nameof(minimumUapVersion));
            }

            var translations = Translate(
                lottieComposition: lottieComposition,
                targetUapVersion: targetUapVersion,
                minimumUapVersion: minimumUapVersion,
                strictTranslation: strictTranslation,
                addCodegenDescriptions: addCodegenDescriptions).ToArray();

            // Combine the issues that are the same in multiple versions into issues with a version range.
            var dict = new Dictionary<TranslationIssue, UapVersionRange>();
            foreach (var (translationResult, versionRange) in translations)
            {
                // Normalize the versionRange so we don't end up with multiple representations
                // of the same range (e.g. (7,8) and (null,8)).
                versionRange.NormalizeForMinimumVersion(LowestValidUapVersion);

                foreach (var issue in translationResult.TranslationIssues)
                {
                    if (!dict.TryGetValue(issue, out var range))
                    {
                        // Issue hasn't been seen before. Add it along with the version range for its translation.
                        dict.Add(issue, versionRange);
                    }
                    else
                    {
                        // Existing issue. Extends its range. We rely on the translations
                        // being ordered from newest UAP version to oldest UAP version, so
                        // we only need to adjust the Start value of the range.
                        range.Start = versionRange.Start;

                        range.NormalizeForMinimumVersion(LowestValidUapVersion);

                        // Update the issue with the extended range.
                        dict[issue] = range;
                    }
                }
            }

            var versionDependentIssues =
                (from pair in dict
                 let issue = pair.Key
                 orderby issue.Code, issue.Description
                 select (issue, pair.Value)).ToArray();

            var translationResults = translations.Select(t => t.translationResult).ToArray();

            return (translationResults, versionDependentIssues);
        }

        static IEnumerable<(TranslationResult translationResult, UapVersionRange versionRange)> Translate(
            LottieComposition lottieComposition,
            uint targetUapVersion,
            uint minimumUapVersion,
            bool strictTranslation,
            bool addCodegenDescriptions)
        {
            // First, generate code for the target version.
            var translationResult =
                LottieToWinCompTranslator.TryTranslateLottieComposition(
                    lottieComposition,
                    targetUapVersion,
                    strictTranslation,
                    addCodegenDescriptions);

            yield return (
                translationResult,
                new UapVersionRange { Start = translationResult.MinimumRequiredUapVersion }
                );

            if (translationResult.RootVisual == null)
            {
                // Failed to translate for the target version. Give up.
                yield break;
            }

            // Produce translations for the next lower version until one is produced
            // that satisfies minimumUapVersion.
            while (translationResult.MinimumRequiredUapVersion > minimumUapVersion)
            {
                var nextLowerTarget = translationResult.MinimumRequiredUapVersion - 1;
                translationResult =
                    LottieToWinCompTranslator.TryTranslateLottieComposition(
                        lottieComposition,
                        nextLowerTarget,
                        strictTranslation,
                        addCodegenDescriptions);

                yield return (
                    translationResult,
                    new UapVersionRange { Start = translationResult.MinimumRequiredUapVersion, End = nextLowerTarget }
                    );
            }
        }
    }
}
