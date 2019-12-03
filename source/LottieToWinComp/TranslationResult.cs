// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;
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
            IEnumerable<TranslationIssue> translationIssues,
            uint minimumRequiredUapVersion,
            GenericDataMap sourceMetadata)
        {
            RootVisual = rootVisual;
            TranslationIssues = translationIssues.ToArray();
            MinimumRequiredUapVersion = minimumRequiredUapVersion;
            SourceMetadata = sourceMetadata;
        }

        /// <summary>
        /// The <see cref="Visual"/> at the root of the translation, or null if the translation failed.
        /// </summary>
        public Visual RootVisual { get; }

        /// <summary>
        /// Metadata from the source.
        /// </summary>
        public GenericDataMap SourceMetadata { get; }

        /// <summary>
        /// The list of issues discovered during translation.
        /// </summary>
        public IReadOnlyList<TranslationIssue> TranslationIssues { get; }

        /// <summary>
        /// The minimum version of UAP required to instantiate the result of the translation.
        /// </summary>
        public uint MinimumRequiredUapVersion { get; }

        // Returns a TranslationResult with the same contents as this but a different root visual.
        internal TranslationResult WithDifferentRoot(Visual rootVisual)
            => new TranslationResult(rootVisual, TranslationIssues, MinimumRequiredUapVersion, SourceMetadata);
    }
}
