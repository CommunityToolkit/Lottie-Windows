// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

#if DEBUG
// Uncomment this to slow down async awaits for testing.
//#define SlowAwaits
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// Handles loading a composition from a Lottie file. The result of the load
    /// is a <see cref="ContentFactory"/> that can be used to instantiate a
    /// Composition tree that will render the Lottie.
    /// </summary>
    abstract class Loader
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        // Private constructor prevents subclassing outside of this class.
        Loader()
        {
        }

        private protected abstract Task<(string?, Stream?)> GetJsonStreamAsync();

        // Asynchronously loads WinCompData from a Lottie file.
        internal async Task<ContentFactory> LoadAsync(LottieVisualOptions options)
        {
            LottieVisualDiagnostics? diagnostics = null;
            var timeMeasurer = TimeMeasurer.Create();

            if (options.HasFlag(LottieVisualOptions.IncludeDiagnostics))
            {
                diagnostics = new LottieVisualDiagnostics { Options = options };
            }

            var result = new ContentFactory(diagnostics);

            // Get the file name and JSON contents.
            (var fileName, var jsonStream) = await GetJsonStreamAsync();

            if (diagnostics != null)
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
            await CheckedAwaitAsync(Task.Run(() =>
            {
                lottieComposition =
                    LottieCompositionReader.ReadLottieCompositionFromJsonStream(
                        jsonStream,
                        LottieCompositionReader.Options.IgnoreMatchNames,
                        out var readerIssues);

                if (diagnostics != null)
                {
                    diagnostics.JsonParsingIssues = ToIssues(readerIssues);
                }
            }));

            if (diagnostics != null)
            {
                diagnostics.ParseTime = timeMeasurer.GetElapsedAndRestart();
            }

            if (lottieComposition is null)
            {
                // Failed to load...
                return result;
            }

            if (diagnostics != null)
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
            await CheckedAwaitAsync(Task.Run(() =>
            {
                // Generate property bindings only if the diagnostics object was requested.
                // This is because the binding information is output in the diagnostics object
                // so there's no point translating bindings if the diagnostics object
                // isn't available.
                var makeColorsBindable = diagnostics != null && options.HasFlag(LottieVisualOptions.BindableColors);
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

                if (diagnostics != null)
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
                if (wincompDataRootVisual != null && optimizationEnabled)
                {
                    // Optimize.
                    wincompDataRootVisual = UIData.Tools.Optimizer.Optimize(wincompDataRootVisual, ignoreCommentProperties: true);

                    if (diagnostics != null)
                    {
                        diagnostics.OptimizationTime = timeMeasurer.GetElapsedAndRestart();
                    }
                }
            }));

            if (wincompDataRootVisual is null)
            {
                // Failed.
                return result;
            }
            else
            {
                if (diagnostics != null)
                {
                    // Save the root visual so diagnostics can generate XML and codegen.
                    diagnostics.RootVisual = wincompDataRootVisual;
                    diagnostics.RequiredUapVersion = requiredUapVersion;
                }

                result.SetRootVisual(wincompDataRootVisual);
                return result;
            }
        }

        static IReadOnlyList<Issue> ToIssues(IEnumerable<(string Code, string Description)> issues)
            => issues.Select(issue => new Issue(code: issue.Code, description: issue.Description)).ToArray();

        static IReadOnlyList<Issue> ToIssues(IEnumerable<TranslationIssue> issues)
            => issues.Select(issue => new Issue(code: issue.Code, description: issue.Description)).ToArray();

        static async Task<(string?, Stream?)> GetStorageFileStreamAsync(StorageFile storageFile)
        {
            var randomAccessStream = await storageFile.OpenReadAsync();
            return (storageFile.Name, randomAccessStream.AsStreamForRead());
        }

        /// <summary>
        /// Gets the highest UAP version supported by the current process.
        /// </summary>
        /// <returns>The highest UAP version supported by the current process.</returns>
        static uint GetCurrentUapVersion()
        {
            // Start testing on version 2. We know that at least version 1 is supported because
            // we are running in UAP code.
            var versionToTest = 2u;

            // Keep querying until IsApiContractPresent fails to find the version.
            while (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", (ushort)versionToTest))
            {
                // Keep looking ...
                versionToTest++;
            }

            // Query failed on versionToTest. Return the previous version.
            return versionToTest - 1;
        }

        [Conditional("DEBUG")]
        static void AssertNotNull<T>(T obj)
            where T : class
        {
            if (obj is null)
            {
                Debug.Assert(obj != null, "Unexpected null");
            }
        }

        // A loader that loads from an IInputStream.
        internal sealed class FromInputStream : Loader
        {
            readonly IInputStream _inputStream;

            internal FromInputStream(IInputStream inputStream)
            {
                AssertNotNull(inputStream);
                _inputStream = inputStream;
            }

            // Turn off the warning about lacking an await. This method has to return a Task
            // and the easiest way to do that when you do not need the asynchrony is to declare
            // the method as async and return the value. This will cause C# to wrap the value in
            // a Task.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            private protected override async Task<(string?, Stream?)> GetJsonStreamAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            {
                return (string.Empty, _inputStream.AsStreamForRead());
            }
        }

        // A loader that loads from a StorageFile.
        internal sealed class FromStorageFile : Loader
        {
            readonly StorageFile _storageFile;

            internal FromStorageFile(StorageFile storageFile)
            {
                AssertNotNull(storageFile);
                _storageFile = storageFile;
            }

            private protected override Task<(string?, Stream?)> GetJsonStreamAsync() =>
                GetStorageFileStreamAsync(_storageFile);
        }

        // A loader that loads from a Uri.
        internal sealed class FromUri : Loader
        {
            readonly Uri _uri;

            internal FromUri(Uri uri)
            {
                AssertNotNull(uri);
                _uri = uri;
            }

            private protected override async Task<(string?, Stream?)> GetJsonStreamAsync()
            {
                var absoluteUri = Uris.GetAbsoluteUri(_uri);
                if (absoluteUri != null)
                {
                    if (absoluteUri.Scheme.StartsWith("ms-"))
                    {
                        return await GetStorageFileStreamAsync(await StorageFile.GetFileFromApplicationUriAsync(absoluteUri));
                    }
                    else
                    {
                        var winrtClient = new Windows.Web.Http.HttpClient();
                        var response = await winrtClient.GetAsync(absoluteUri);

                        var result = await response.Content.ReadAsInputStreamAsync();
                        return (absoluteUri.LocalPath, result.AsStreamForRead());
                    }
                }

                return (null, null);
            }
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

        // For testing purposes, slows down a task.
#if SlowAwaits
        const int _checkedDelayMs = 5;
        async
#endif
        static Task CheckedAwaitAsync(Task task)
        {
#if SlowAwaits
            await Task.Delay(_checkedDelayMs);
            await task;
            await Task.Delay(_checkedDelayMs);
#else
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            return task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
#endif
        }
    }
}