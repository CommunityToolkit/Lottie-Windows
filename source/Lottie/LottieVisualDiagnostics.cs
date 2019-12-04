// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Diagnostics information about a Lottie and its translation.
    /// </summary>
    public sealed class LottieVisualDiagnostics
    {
        public string FileName { get; internal set; } = string.Empty;

        public string SuggestedFileName =>
            string.IsNullOrWhiteSpace(FileName)
                ? "MyComposition"
                : System.IO.Path.GetFileNameWithoutExtension(FileName);

        public string SuggestedClassName
        {
            get
            {
                string result = null;
                if (LottieComposition != null)
                {
                    result = InstantiatorGeneratorBase.TrySynthesizeClassName(LottieComposition.Name);
                }

                return result ?? InstantiatorGeneratorBase.TrySynthesizeClassName(SuggestedFileName);
            }
        }

        public TimeSpan Duration => LottieComposition?.Duration ?? TimeSpan.Zero;

        public TimeSpan ReadTime { get; internal set; }

        public TimeSpan ParseTime { get; internal set; }

        public TimeSpan ValidationTime { get; internal set; }

        public TimeSpan OptimizationTime { get; internal set; }

        public TimeSpan TranslationTime { get; internal set; }

        public TimeSpan InstantiationTime { get; internal set; }

        public IEnumerable<Issue> JsonParsingIssues { get; internal set; } = Array.Empty<Issue>();

        public IEnumerable<Issue> LottieValidationIssues { get; internal set; } = Array.Empty<Issue>();

        public IEnumerable<Issue> TranslationIssues { get; internal set; } = Array.Empty<Issue>();

        public double LottieWidth => LottieComposition?.Width ?? 0;

        public double LottieHeight => LottieComposition?.Height ?? 0;

        public string LottieDetails => DescribeLottieComposition();

        public string LottieVersion => LottieComposition?.Version.ToString() ?? string.Empty;

        /// <summary>
        /// Gets the options that were set on the <see cref="LottieVisualSource"/> when it
        /// produced this diagnostics object.
        /// </summary>
        public LottieVisualOptions Options { get; internal set; }

        public string GenerateLottieXml()
        {
            if (LottieComposition == null) { return null; }
            return LottieData.Serialization.LottieCompositionXmlSerializer.ToXml(LottieComposition).ToString();
        }

        public string GenerateWinCompXml()
        {
            return WinCompData.Tools.CompositionObjectXmlSerializer.ToXml(RootVisual).ToString();
        }

        public string GenerateCSharpCode()
        {
            if (LottieComposition == null) { return null; }

            return CSharpInstantiatorGenerator.CreateFactoryCode(
                SuggestedClassName,
                new (CompositionObject, uint)[] { (RootVisual, RequiredUapVersion) },
                SourceMetadata,
                (float)LottieComposition.Width,
                (float)LottieComposition.Height,
                LottieComposition.Duration,
                disableFieldOptimization: false).csText;
        }

        public void GenerateCxCode(string headerFileName, out string cppText, out string hText)
        {
            if (LottieComposition == null)
            {
                cppText = null;
                hText = null;
                return;
            }

            (string cppText, string hText, IEnumerable<Uri> assetList) generatedCode = CxInstantiatorGenerator.CreateFactoryCode(
                SuggestedClassName,
                new (CompositionObject, uint)[] { (RootVisual, RequiredUapVersion) },
                SourceMetadata,
                (float)LottieComposition.Width,
                (float)LottieComposition.Height,
                LottieComposition.Duration,
                headerFileName,
                disableFieldOptimization: false);

            cppText = generatedCode.cppText;
            hText = generatedCode.hText;
        }

        public KeyValuePair<string, double>[] Markers { get; internal set; } = Array.Empty<KeyValuePair<string, double>>();

        // Holds the parsed LottieComposition. Only used if one of the codegen or XML options was selected.
        internal LottieComposition LottieComposition { get; set; }

        internal GenericDataMap SourceMetadata { get; set; }

        // Holds the translated Visual. Only used if one of the codegen or XML options was selected.
        internal WinCompData.Visual RootVisual { get; set; }

        // The UAP version required by the translated code. Only used if one of the codegen or
        // XML options was selected.
        internal uint RequiredUapVersion { get; set; }

        internal LottieVisualDiagnostics Clone() =>
            new LottieVisualDiagnostics
            {
                FileName = FileName,
                InstantiationTime = InstantiationTime,
                JsonParsingIssues = JsonParsingIssues,
                LottieComposition = LottieComposition,
                LottieValidationIssues = LottieValidationIssues,
                Markers = Markers,
                Options = Options,
                ParseTime = ParseTime,
                ReadTime = ReadTime,
                RootVisual = RootVisual,
                TranslationTime = TranslationTime,
                ValidationTime = ValidationTime,
                TranslationIssues = TranslationIssues,
            };

        // Creates a string that describes the Lottie.
        string DescribeLottieComposition()
        {
            if (LottieComposition == null) { return null; }

            var stats = new LottieData.Tools.Stats(LottieComposition);

            return $"LottieVisualSource w={LottieComposition.Width} h={LottieComposition.Height} " +
                $"layers: precomp={stats.PreCompLayerCount} solid={stats.SolidLayerCount} " +
                $"image={stats.ImageLayerCount} null={stats.NullLayerCount} " +
                $"shape={stats.ShapeLayerCount} text={stats.TextLayerCount}";
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
