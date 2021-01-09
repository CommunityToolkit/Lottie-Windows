// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Translation;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
#if PUBLIC_IRToWinComp
    public
#endif
    sealed class IRToMultiVersionWinCompTranslator
    {
        // Lowest version of UAP we will produce code for. Version 7 is required for
        // Shapes to work completely (they were added in 6 but had some issues until 7).
        const uint LowestValidUapVersion = 7;

        /// <summary>
        /// Attempts to translate the given <see cref="IRComposition"/> for a range of UAP versions,
        /// producing one or more translations.
        /// </summary>
        /// <param name="irComposition">The <see cref="IRComposition"/> to translate.</param>
        /// <param name="configuration">Controls optional features of the translator.</param>
        /// <param name="minimumUapVersion">The lowest version of UAP on which the result must run.
        /// Must be &gt;= 7 and &lt;= the target UAP version.</param>
        /// <returns>The results of the translation and the issues.</returns>
        public static MultiVersionTranslationResult TryTranslateLottieComposition(
            IRComposition irComposition,
            in TranslatorConfiguration configuration,
            uint minimumUapVersion)
        {
            if (configuration.TargetUapVersion < LowestValidUapVersion)
            {
                throw new ArgumentException(nameof(configuration.TargetUapVersion));
            }

            if (minimumUapVersion > configuration.TargetUapVersion ||
                minimumUapVersion < LowestValidUapVersion)
            {
                throw new ArgumentException(nameof(minimumUapVersion));
            }

            var translations = Translate(
                irComposition: irComposition,
                configuration: configuration,
                minimumUapVersion: minimumUapVersion).ToArray();

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

            return new MultiVersionTranslationResult(
                translationResults: translations.Select(t => t.translationResult),
                issues: from pair in dict
                        let issue = pair.Key
                        orderby issue.Code, issue.Description
                        select (issue, pair.Value));
        }

        static IEnumerable<(TranslationResult translationResult, UapVersionRange versionRange)> Translate(
            IRComposition irComposition,
            TranslatorConfiguration configuration,
            uint minimumUapVersion)
        {
            // First, generate code for the target version.
            var translationResult =
                IRToWinCompTranslator.TryTranslateLottieComposition(
                    irComposition,
                    configuration: configuration);

            yield return (
                translationResult,
                new UapVersionRange { Start = translationResult.MinimumRequiredUapVersion }
                );

            if (translationResult.RootVisual is null)
            {
                // Failed to translate for the target version. Give up.
                yield break;
            }

            // Produce translations for the next lower version until one is produced
            // that satisfies minimumUapVersion.
            while (translationResult.MinimumRequiredUapVersion > minimumUapVersion)
            {
                // Copy the configuration but change the target version to the next value less than
                // the version supported by the previous translation.
                var nextLowerTargetConfiguration = configuration;
                nextLowerTargetConfiguration.TargetUapVersion = translationResult.MinimumRequiredUapVersion - 1;

                translationResult =
                    IRToWinCompTranslator.TryTranslateLottieComposition(
                        irComposition,
                        configuration: nextLowerTargetConfiguration);

                yield return (
                    translationResult,
                    new UapVersionRange
                    {
                        Start = translationResult.MinimumRequiredUapVersion,
                        End = nextLowerTargetConfiguration.TargetUapVersion,
                    });
            }
        }
    }
}
