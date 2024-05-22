// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Lottie.CompMetadata;
using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.LottieData.Optimization;
using CommunityToolkit.WinUI.Lottie.LottieData.Serialization;
using CommunityToolkit.WinUI.Lottie.LottieToWinComp;
using Windows.Foundation.Metadata;

#if WINAPPSDK
using Microsoft.UI.Composition;
#else
using Windows.UI.Composition;
#endif

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// Handles loading a composition from a Lottie file. The result of the load
    /// is a <see cref="AnimatedVisualFactory"/> that can be used to instantiate a
    /// Composition tree that will render the Lottie.
    /// </summary>
    abstract class Loader : IDisposable
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        internal abstract ICompositionSurface? LoadImage(Uri imageUri);

        /// <summary>
        /// Asynchonously loads an <see cref="AnimatedVisualFactory"/> that can be
        /// used to instantiate IAnimatedVisual instances.
        /// </summary>
        /// <param name="jsonLoader">A delegate that asynchronously loads the JSON for
        /// a Lottie file.</param>
        /// <param name="imageLoader">A delegate that loads images that support a Lottie file.</param>
        /// <param name="options">Options.</param>
        /// <returns>An <see cref="AnimatedVisualFactory"/> that can be used
        /// to instantiate IAnimatedVisual instances.</returns>
        private protected static async Task<AnimatedVisualFactory?> LoadAsync(
            Func<Task<(string? name, Stream? stream)>> jsonLoader,
            Loader imageLoader,
            LottieVisualOptions options)
        {
            LottieVisualDiagnostics? diagnostics = null;
            var timeMeasurer = TimeMeasurer.Create();

            if (options.HasFlag(LottieVisualOptions.IncludeDiagnostics))
            {
                diagnostics = new LottieVisualDiagnostics { Options = options };
            }

            var result = new AnimatedVisualFactory(imageLoader, diagnostics);

            try
            {
                // Get the file name and JSON contents.
                (var fileName, var jsonStream) = await jsonLoader();

                if (diagnostics is not null)
                {
                    diagnostics.FileName = fileName ?? string.Empty;
                    diagnostics.ReadTime = timeMeasurer.GetElapsedAndRestart();
                }

                if (jsonStream is null)
                {
                    // Failed to load ...
                    return result;
                }

                // Parsing large Lottie files can take significant time. Do it on
                // another thread.
                LottieComposition? lottieComposition = null;
                await Task.Run(() =>
                {
                    lottieComposition =
                        LottieCompositionReader.ReadLottieCompositionFromJsonStream(
                            jsonStream,
                            LottieCompositionReader.Options.IgnoreMatchNames,
                            out var readerIssues);

                    if (lottieComposition is not null && options.HasFlag(LottieVisualOptions.Optimize))
                    {
                        lottieComposition = LottieMergeOptimizer.Optimize(lottieComposition);
                    }

                    if (diagnostics is not null)
                    {
                        diagnostics.JsonParsingIssues = ToIssues(readerIssues);
                    }
                });

                if (diagnostics is not null)
                {
                    diagnostics.ParseTime = timeMeasurer.GetElapsedAndRestart();
                }

                if (lottieComposition is null)
                {
                    // Failed to load...
                    return result;
                }

                if (diagnostics is not null)
                {
                    // Save the LottieComposition in the diagnostics so that the xml and codegen
                    // code can be derived from it.
                    diagnostics.LottieComposition = lottieComposition;

                    // Validate the composition and report if issues are found.
                    diagnostics.LottieValidationIssues = ToIssues(LottieCompositionValidator.Validate(lottieComposition));
                    diagnostics.ValidationTime = timeMeasurer.GetElapsedAndRestart();
                }

                result.SetDimensions(
                    width: lottieComposition.Width,
                    height: lottieComposition.Height,
                    duration: lottieComposition.Duration);

                // Translating large Lotties can take significant time. Do it on another thread.
                WinCompData.Visual? wincompDataRootVisual = null;
                uint requiredUapVersion = 0;
                var optimizationEnabled = options.HasFlag(LottieVisualOptions.Optimize);

                TranslationResult translationResult;
                await Task.Run(() =>
                {
                    // Generate property bindings only if the diagnostics object was requested.
                    // This is because the binding information is output in the diagnostics object
                    // so there's no point translating bindings if the diagnostics object
                    // isn't available.
                    var makeColorsBindable = diagnostics is not null && options.HasFlag(LottieVisualOptions.BindableColors);
                    translationResult = LottieToWinCompTranslator.TryTranslateLottieComposition(
                        lottieComposition: lottieComposition,
                        configuration: new TranslatorConfiguration
                        {
                            TranslatePropertyBindings = makeColorsBindable,
                            GenerateColorBindings = makeColorsBindable,
                            TargetUapVersion = GetCurrentUapVersion(),
                        });

                    wincompDataRootVisual = translationResult.RootVisual;
                    requiredUapVersion = translationResult.MinimumRequiredUapVersion;

                    if (diagnostics is not null)
                    {
                        diagnostics.TranslationIssues = ToIssues(translationResult.TranslationIssues);
                        diagnostics.TranslationTime = timeMeasurer.GetElapsedAndRestart();

                        // If there were any property bindings, save them in the Diagnostics object.
                        if (translationResult.SourceMetadata.TryGetValue(s_propertyBindingNamesKey, out var propertyBindingNames))
                        {
                            diagnostics.ThemePropertyBindings = (IReadOnlyList<PropertyBinding>)propertyBindingNames;
                        }
                    }

                    // Optimize the resulting translation. This will usually significantly reduce the size of
                    // the Composition code, however it might slow down loading too much on complex Lotties.
                    if (wincompDataRootVisual is not null && optimizationEnabled)
                    {
                        // Optimize.
                        wincompDataRootVisual = UIData.Tools.Optimizer.Optimize(wincompDataRootVisual, ignoreCommentProperties: true);

                        if (diagnostics is not null)
                        {
                            diagnostics.OptimizationTime = timeMeasurer.GetElapsedAndRestart();
                        }
                    }
                });

                if (wincompDataRootVisual is null)
                {
                    // Failed.
                    return result;
                }
                else
                {
                    if (diagnostics is not null)
                    {
                        // Save the root visual so diagnostics can generate XML and codegen.
                        diagnostics.RootVisual = wincompDataRootVisual;
                        diagnostics.RequiredUapVersion = requiredUapVersion;
                    }

                    result.SetRootVisual(wincompDataRootVisual);
                    return result;
                }
            }
            catch
            {
                // Swallow exceptions. There's nowhere to report them.
            }

            return result;
        }

        static IReadOnlyList<Issue> ToIssues(IEnumerable<(string Code, string Description)> issues)
            => issues.Select(issue => new Issue(code: issue.Code, description: issue.Description)).ToArray();

        static IReadOnlyList<Issue> ToIssues(IEnumerable<TranslationIssue> issues)
            => issues.Select(issue => new Issue(code: issue.Code, description: issue.Description)).ToArray();

        /// <summary>
        /// Gets the highest UAP version supported by the current process.
        /// </summary>
        /// <returns>The highest UAP version supported by the current process.</returns>
        static uint GetCurrentUapVersion()
        {
            // Start testing on version 2. We know that at least version 1 is supported because
            // we are running in UAP code.
            var versionToTest = 1u;

            // Keep querying until IsApiContractPresent fails to find the version.
            while (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", (ushort)(versionToTest + 1)))
            {
                // Keep looking ...
                versionToTest++;
            }

            // TODO: we do not support UAP above 14 in Lottie-Windows yet, only in LottieGen.
            versionToTest = Math.Min(versionToTest, 14);

            // Query failed on versionToTest. Return the previous version.
            return versionToTest;
        }

        // Specializes the Stopwatch to do just the one thing we need of it - get the time
        // elapsed since the last call then restart the Stopwatch to start measuring again.
        readonly struct TimeMeasurer
        {
            readonly Stopwatch _stopwatch;

            TimeMeasurer(Stopwatch stopwatch) => _stopwatch = stopwatch;

            public static TimeMeasurer Create() => new TimeMeasurer(Stopwatch.StartNew());

            public TimeSpan GetElapsedAndRestart()
            {
                var result = _stopwatch.Elapsed;
                _stopwatch.Restart();
                return result;
            }
        }

        public abstract void Dispose();
    }
}