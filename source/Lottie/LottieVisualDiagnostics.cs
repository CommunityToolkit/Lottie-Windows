// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

#if Lottie_Windows_WinUI3
using Microsoft.UI.Composition;
using MicrosoftToolkit.WinUI.Lottie;
#else
using Windows.UI.Composition;
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Diagnostics information about a Lottie and its translation.
    /// </summary>
    sealed class LottieVisualDiagnostics
    {
        public string FileName { get; internal set; } = string.Empty;

        public TimeSpan Duration => LottieComposition?.Duration ?? TimeSpan.Zero;

        public TimeSpan ReadTime { get; internal set; }

        public TimeSpan ParseTime { get; internal set; }

        public TimeSpan ValidationTime { get; internal set; }

        public TimeSpan OptimizationTime { get; internal set; }

        public TimeSpan TranslationTime { get; internal set; }

        public TimeSpan InstantiationTime { get; internal set; }

        public IReadOnlyList<Issue> JsonParsingIssues { get; internal set; } = Array.Empty<Issue>();

        public IReadOnlyList<Issue> LottieValidationIssues { get; internal set; } = Array.Empty<Issue>();

        public IReadOnlyList<Issue> TranslationIssues { get; internal set; } = Array.Empty<Issue>();

        public double LottieWidth => LottieComposition?.Width ?? 0;

        public double LottieHeight => LottieComposition?.Height ?? 0;

        /// <summary>
        /// Gets the options that were set on the <see cref="LottieVisualSource"/> when it
        /// produced this diagnostics object.
        /// </summary>
        public LottieVisualOptions Options { get; internal set; }

        // Holds the parsed LottieComposition.
        internal LottieComposition? LottieComposition { get; set; }

        // Holds the translated Visual. Only used if one of the codegen or XML options was selected.
        internal WinCompData.Visual? RootVisual { get; set; }

        // The UAP version required by the translated code. Only used if one of the codegen or
        // XML options was selected.
        internal uint RequiredUapVersion { get; set; }

        // CompostionPropertySet that holds the theming properties.
        internal CompositionPropertySet? ThemingPropertySet { get; set; }

        // Describes the property bindings in the ThemingPropertySet.
        internal IReadOnlyList<PropertyBinding>? ThemePropertyBindings { get; set; }

        internal LottieVisualDiagnostics Clone() =>
            new LottieVisualDiagnostics
            {
                FileName = FileName,
                InstantiationTime = InstantiationTime,
                JsonParsingIssues = JsonParsingIssues,
                LottieComposition = LottieComposition,
                LottieValidationIssues = LottieValidationIssues,
                Options = Options,
                ParseTime = ParseTime,
                ReadTime = ReadTime,
                RequiredUapVersion = RequiredUapVersion,
                RootVisual = RootVisual,
                ThemePropertyBindings = ThemePropertyBindings,
                ThemingPropertySet = ThemingPropertySet,
                TranslationIssues = TranslationIssues,
                TranslationTime = TranslationTime,
                ValidationTime = ValidationTime,
            };
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
