// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if DEBUG
// Uncomment this to slow down async awaits for testing.
//#define SlowAwaits
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Microsoft.Toolkit.Uwp.UI.Lottie
{
    /// <summary>
    /// An <see cref="IAnimatedVisualSource"/> for a Lottie composition. This allows
    /// a Lottie to be specified as the source for a <see cref="AnimatedVisualPlayer"/>.
    /// </summary>
    public sealed class LottieVisualSource : DependencyObject, IDynamicAnimatedVisualSource
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        readonly StorageFile _storageFile;
        EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>> _compositionInvalidatedEventTokenTable;
        int _loadVersion;
        Uri _uriSource;
        ContentFactory _contentFactory;

        /// <summary>
        /// Gets the options for the <see cref="LottieVisualSource"/>.
        /// </summary>
        // Optimize Lotties by default. Optimization takes a little longer but usually produces much
        // more efficient translations. The only reason someone would turn optimization off is if
        // the time to translate is too high, but in that case the Lottie is probably going to perform
        // so badly on the machine that it won't really be usable with our without optimization.
        public static DependencyProperty OptionsProperty { get; } =
            RegisterDp(nameof(Options), LottieVisualOptions.Optimize);

        /// <summary>
        /// Gets the URI from which to load a JSON Lottie file.
        /// </summary>
        public static DependencyProperty UriSourceProperty { get; } =
            RegisterDp<Uri>(nameof(UriSource), null,
            (owner, oldValue, newValue) => owner.HandleUriSourcePropertyChanged(oldValue, newValue));

        static DependencyProperty RegisterDp<T>(string propertyName, T defaultValue) =>
            DependencyProperty.Register(propertyName, typeof(T), typeof(LottieVisualSource), new PropertyMetadata(defaultValue));

        static DependencyProperty RegisterDp<T>(string propertyName, T defaultValue, Action<LottieVisualSource, T, T> callback) =>
            DependencyProperty.Register(propertyName, typeof(T), typeof(LottieVisualSource),
                new PropertyMetadata(defaultValue, (d, e) => callback((LottieVisualSource)d, (T)e.OldValue, (T)e.NewValue)));

        /// <summary>
        /// Initializes a new instance of the <see cref="LottieVisualSource"/> class to be used in markup.
        /// </summary>
        public LottieVisualSource()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LottieVisualSource"/> class from a <see cref="StorageFile"/>.
        /// </summary>
        public LottieVisualSource(StorageFile storageFile)
        {
            _storageFile = storageFile;
        }

        /// <summary>
        /// Gets or sets options for how the Lottie is loaded.
        /// </summary>
        public LottieVisualOptions Options
        {
            get => (LottieVisualOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        /// <summary>
        /// Gets or sets the Uniform Resource Identifier (URI) of the JSON source file for this <see cref="LottieVisualSource"/>.
        /// </summary>
        public Uri UriSource
        {
            get => (Uri)GetValue(UriSourceProperty);
            set => SetValue(UriSourceProperty, value);
        }

        /// <summary>
        /// Called by XAML to convert a string to an <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <returns>The <see cref="LottieVisualSource"/> for the given url.</returns>
        public static LottieVisualSource CreateFromString(string uri)
        {
            var uriUri = StringToUri(uri);
            if (uriUri is null)
            {
                return null;
            }

            return new LottieVisualSource { UriSource = uriUri };
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSource"/>.
        /// </summary>
        /// <param name="file">A file that refers to a JSON Lottie file.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        [DefaultOverload]
        public IAsyncAction SetSourceAsync(StorageFile file)
        {
            _uriSource = null;
            return LoadAsync(file is null ? null : new Loader(this, file)).AsAsyncAction();
        }

        /// <summary>
        /// Sets the source for the <see cref="LottieVisualSource"/>.
        /// </summary>
        /// <param name="sourceUri">A URI that refers to a JSON Lottie file.</param>
        /// <returns>An <see cref="IAsyncAction"/> that completes when the load completes or fails.</returns>
        public IAsyncAction SetSourceAsync(Uri sourceUri)
        {
            _uriSource = sourceUri;

            // Update the dependency property to keep it in sync with _uriSource.
            // This will not trigger loading because it will be seen as no change
            // from the current (just set) _uriSource value.
            UriSource = sourceUri;
            return LoadAsync(sourceUri is null ? null : new Loader(this, sourceUri)).AsAsyncAction();
        }

        /// <summary>
        /// Implements <see cref="IDynamicAnimatedVisualSource"/>.
        /// </summary>
        // TODO: currently explicitly implemented interfaces are causing a problem with .NET Native. Make them implicit for now.
        public event TypedEventHandler<IDynamicAnimatedVisualSource, object> AnimatedVisualInvalidated
        {
            add
            {
                return EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>>
                   .GetOrCreateEventRegistrationTokenTable(ref _compositionInvalidatedEventTokenTable)
                   .AddEventHandler(value);
            }

            remove
            {
                EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>>
                   .GetOrCreateEventRegistrationTokenTable(ref _compositionInvalidatedEventTokenTable)
                    .RemoveEventHandler(value);
            }
        }

        /// <summary>
        /// Implements <see cref="IAnimatedVisualSource"/>.
        /// </summary>
        /// <param name="compositor">The <see cref="Compositor"/> that can be used as a factory for the resulting <see cref="IAnimatedVisual"/>.</param>
        /// <param name="diagnostics">An optional object that may provide extra information about the result.</param>
        /// <returns>An <see cref="IAnimatedVisual"/>.</returns>
        // TODO: currently explicitly implemented interfaces are causing a problem with .NET Native. Make them implicit for now.
        //bool IAnimatedVisualSource.TryCreateAnimatedVisual(
        public IAnimatedVisual TryCreateAnimatedVisual(
            Compositor compositor,
            out object diagnostics)
        {
            if (_contentFactory is null)
            {
                // No content has been loaded yet.
                // Return an IAnimatedVisual that produces nothing.
                diagnostics = null;
                return new Comp();
            }
            else
            {
                // Some content was loaded. Ask the contentFactory to produce an
                // IAnimatedVisual. If it returns null, the player will treat it
                // as an error.
                return _contentFactory.TryCreateAnimatedVisual(compositor, out diagnostics);
            }
        }

        void NotifyListenersThatCompositionChanged()
        {
            EventRegistrationTokenTable<TypedEventHandler<IDynamicAnimatedVisualSource, object>>
                .GetOrCreateEventRegistrationTokenTable(ref _compositionInvalidatedEventTokenTable)
                .InvocationList?.Invoke(this, null);
        }

        // Called when the UriSource property is updated.
        void HandleUriSourcePropertyChanged(Uri oldValue, Uri newValue)
        {
            if (newValue == _uriSource)
            {
                // Ignore if setting to the current value. This can't happen if the value
                // is being set via the DependencyProperty, but it will happen if the value
                // is set via SetSourceAsync, as _uriSource will have been set before this
                // is called.
                return;
            }

            _uriSource = newValue;

            var ignoredTask = StartLoadingAndIgnoreErrorsAsync();

            async Task StartLoadingAndIgnoreErrorsAsync()
            {
                try
                {
                    await LoadAsync(new Loader(this, UriSource));
                }
                catch
                {
                    // Swallow any errors - nobody is listening.
                }
            }
        }

        // Starts loading. Completes the returned task when the load completes or is replaced by another load.
        async Task LoadAsync(Loader loader)
        {
            var loadVersion = ++_loadVersion;

            var oldContentFactory = _contentFactory;
            _contentFactory = null;

            if (oldContentFactory != null)
            {
                // Notify all listeners that their existing content is no longer valid.
                // They should stop showing the content. We will notify them again when the
                // content changes.
                NotifyListenersThatCompositionChanged();
            }

            if (loader is null)
            {
                // No loader means clear out what you previously loaded.
                return;
            }

            ContentFactory contentFactory;
            try
            {
                contentFactory = await loader.LoadAsync(Options);
            }
            catch
            {
                // Set the content factory to one that will return a null IAnimatedVisual to
                // indicate that something went wrong. If the load succeeds this will get overwritten.
                contentFactory = ContentFactory.FailedContent;
            }

            if (loadVersion != _loadVersion)
            {
                // Another load request came in before this one completed.
                return;
            }

            if (contentFactory is null)
            {
                // Load didn't produce anything.
                return;
            }

            // We are the the most recent load. Save the result.
            _contentFactory = contentFactory;

            // Notify all listeners that they should try to create their instance of the content again.
            NotifyListenersThatCompositionChanged();

            if (!contentFactory.CanInstantiate)
            {
                // The load did not produce any content. Throw an exception so the caller knows.
                throw new ArgumentException("Failed to load animated visual.");
            }
        }

        static Issue[] ToIssues(IEnumerable<(string Code, string Description)> issues)
            => issues.Select(issue => new Issue { Code = issue.Code, Description = issue.Description }).ToArray();

        static Issue[] ToIssues(IEnumerable<TranslationIssue> issues)
            => issues.Select(issue => new Issue { Code = issue.Code, Description = issue.Description }).ToArray();

        // Handles loading a composition from a Lottie file.
        sealed class Loader
        {
            readonly LottieVisualSource _owner;
            readonly Uri _uri;
            readonly StorageFile _storageFile;

            internal Loader(LottieVisualSource owner, Uri uri)
            {
                _owner = owner;
                _uri = uri;
            }

            internal Loader(LottieVisualSource owner, StorageFile storageFile)
            {
                _owner = owner;
                _storageFile = storageFile;
            }

            // Asynchronously loads WinCompData from a Lottie file.
            internal async Task<ContentFactory> LoadAsync(LottieVisualOptions options)
            {
                if (_uri is null && _storageFile is null)
                {
                    // Request to load null. Return a null ContentFactory.
                    return null;
                }

                LottieVisualDiagnostics diagnostics = null;
                Stopwatch sw = null;
                if (options.HasFlag(LottieVisualOptions.IncludeDiagnostics))
                {
                    sw = Stopwatch.StartNew();
                    diagnostics = new LottieVisualDiagnostics { Options = options };
                }

                var result = new ContentFactory(diagnostics);

                // Get the file name and contents.
                (var fileName, var jsonStream) = await GetFileStreamAsync();

                if (diagnostics != null)
                {
                    diagnostics.FileName = fileName;
                    diagnostics.ReadTime = sw.Elapsed;
                    sw.Restart();
                }

                if (jsonStream is null)
                {
                    // Failed to load ...
                    return result;
                }

                // Parsing large Lottie files can take significant time. Do it on
                // another thread.
                LottieData.LottieComposition lottieComposition = null;
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
                    diagnostics.ParseTime = sw.Elapsed;
                    sw.Restart();
                }

                if (lottieComposition is null)
                {
                    // Failed to load...
                    return result;
                }

                if (diagnostics != null)
                {
                    diagnostics.LottieComposition = lottieComposition;

                    // Validate the composition and report if issues are found.
                    diagnostics.LottieValidationIssues = ToIssues(LottieCompositionValidator.Validate(lottieComposition));
                    diagnostics.ValidationTime = sw.Elapsed;
                    sw.Restart();
                }

                result.SetDimensions(
                    width: lottieComposition.Width,
                    height: lottieComposition.Height,
                    duration: lottieComposition.Duration);

                // Translating large Lotties can take significant time. Do it on another thread.
                WinCompData.Visual wincompDataRootVisual = null;
                uint requiredUapVersion = 0;
                var optimizationEnabled = _owner.Options.HasFlag(LottieVisualOptions.Optimize);

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
                        diagnostics.TranslationTime = sw.Elapsed;

                        // If there were any property bindings, save them in the Diagnostics object.
                        if (translationResult.SourceMetadata.TryGetValue(s_propertyBindingNamesKey, out var propertyBindingNames))
                        {
                            diagnostics.ThemePropertyBindings = (IReadOnlyList<PropertyBinding>)propertyBindingNames;
                        }

                        sw.Restart();
                    }

                    // Optimize the resulting translation. This will usually significantly reduce the size of
                    // the Composition code, however it might slow down loading too much on complex Lotties.
                    if (wincompDataRootVisual != null && optimizationEnabled)
                    {
                        // Optimize.
                        wincompDataRootVisual = UIData.Tools.Optimizer.Optimize(wincompDataRootVisual, ignoreCommentProperties: true);

                        if (diagnostics != null)
                        {
                            diagnostics.OptimizationTime = sw.Elapsed;
                            sw.Restart();
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

            Task<(string, Stream)> GetFileStreamAsync()
                => _storageFile != null
                    ? GetStorageFileStreamAsync(_storageFile)
                    : GetUriStreamAsync(_uri);

            Task<(string, string)> ReadFileAsync()
                    => _storageFile != null
                        ? ReadStorageFileAsync(_storageFile)
                        : ReadUriAsync(_uri);

            async Task<(string, string)> ReadUriAsync(Uri uri)
            {
                var absoluteUri = GetAbsoluteUri(uri);
                if (absoluteUri != null)
                {
                    if (absoluteUri.Scheme.StartsWith("ms-"))
                    {
                        return await ReadStorageFileAsync(await StorageFile.GetFileFromApplicationUriAsync(absoluteUri));
                    }
                    else
                    {
                        var winrtClient = new Windows.Web.Http.HttpClient();
                        var response = await winrtClient.GetAsync(absoluteUri);
                        var result = await response.Content.ReadAsStringAsync();
                        return (absoluteUri.LocalPath, result);
                    }
                }

                return (null, null);
            }

            async Task<(string, Stream)> GetUriStreamAsync(Uri uri)
            {
                var absoluteUri = GetAbsoluteUri(uri);
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

            async Task<(string, string)> ReadStorageFileAsync(StorageFile storageFile)
            {
                Debug.Assert(storageFile != null, "Precondition");
                var result = await FileIO.ReadTextAsync(storageFile);
                return (storageFile.Name, result);
            }

            async Task<(string, Stream)> GetStorageFileStreamAsync(StorageFile storageFile)
            {
                var randomAccessStream = await storageFile.OpenReadAsync();
                return (storageFile.Name, randomAccessStream.AsStreamForRead());
            }
        }

        // Parses a string into an absolute URI, or null if the string is malformed.
        static Uri StringToUri(string uri)
        {
            if (!Uri.IsWellFormedUriString(uri, UriKind.RelativeOrAbsolute))
            {
                return null;
            }

            return GetAbsoluteUri(new Uri(uri, UriKind.RelativeOrAbsolute));
        }

        // Returns an absolute URI. Relative URIs are made relative to ms-appx:///
        static Uri GetAbsoluteUri(Uri uri)
        {
            if (uri is null)
            {
                return null;
            }

            if (uri.IsAbsoluteUri)
            {
                return uri;
            }

            return new Uri($"ms-appx:///{uri}", UriKind.Absolute);
        }

        // Information from which a composition's content can be instantiated. Contains the WinCompData
        // translation of a composition and some metadata.
        sealed class ContentFactory : IAnimatedVisualSource
        {
            internal static readonly ContentFactory FailedContent = new ContentFactory(null);
            readonly LottieVisualDiagnostics _diagnostics;
            WinCompData.Visual _wincompDataRootVisual;
            WinCompData.CompositionPropertySet _wincompDataThemingPropertySet;
            double _width;
            double _height;
            TimeSpan _duration;
            CompositionPropertySet _themingPropertySet;

            internal ContentFactory(LottieVisualDiagnostics diagnostics)
            {
                _diagnostics = diagnostics;
            }

            internal void SetDimensions(double width, double height, TimeSpan duration)
            {
                _width = width;
                _height = height;
                _duration = duration;
            }

            internal void SetRootVisual(WinCompData.Visual rootVisual)
            {
                // Save the root visual.
                _wincompDataRootVisual = rootVisual;

                // Find the theming property set, if any.
                var graph = ObjectGraph<Graph.Node>.FromCompositionObject(_wincompDataRootVisual, includeVertices: false);
                _wincompDataThemingPropertySet = graph.
                                                    CompositionObjectNodes.
                                                    Where(n => n.Object is WinCompData.CompositionPropertySet cps && cps.Owner is null).
                                                    Select(n => (WinCompData.CompositionPropertySet)n.Object).FirstOrDefault();
            }

            internal bool CanInstantiate => _wincompDataRootVisual != null;

            // Clones a new diagnostics object. Will return null if the factory
            // has no diagnostics object.
            LottieVisualDiagnostics GetDiagnosticsClone()
            {
                return _diagnostics?.Clone();
            }

            public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics)
            {
                // Clone the Diagnostics object so that the data from the translation is captured, then we
                // will update the clone with information about this particular instantiation.
                var diags = GetDiagnosticsClone();
                diagnostics = diags;

                if (!CanInstantiate)
                {
                    return null;
                }
                else
                {
                    var sw = Stopwatch.StartNew();

                    var instantiator = new Instantiator(compositor, initialCacheContent: null);

                    var result = new Comp()
                    {
                        RootVisual = (Visual)instantiator.GetInstance(_wincompDataRootVisual),
                        Size = new System.Numerics.Vector2((float)_width, (float)_height),
                        Duration = _duration,
                    };

                    if (diags != null)
                    {
                        if (_wincompDataThemingPropertySet != null && _themingPropertySet is null)
                        {
                            // Instantiate the theming property set. This is shared by all of the instantiations.
                            _themingPropertySet = (CompositionPropertySet)instantiator.GetInstance(_wincompDataThemingPropertySet);
                            diags.ThemingPropertySet = _diagnostics.ThemingPropertySet = _themingPropertySet;
                        }

                        diags.InstantiationTime = sw.Elapsed;
                    }

                    return result;
                }
            }
        }

        /// <summary>
        /// Returns a string representation of the <see cref="LottieVisualSource"/> for debugging purposes.
        /// </summary>
        /// <returns>A string representation of the <see cref="LottieVisualSource"/> for debugging purposes.</returns>
        public override string ToString()
        {
            // TODO - if there's a _contentFactory, it should store the identity and report here
            var identity = (_storageFile != null) ? _storageFile.Name : _uriSource?.ToString() ?? string.Empty;
            return $"LottieVisualSource({identity})";
        }

        /// <summary>
        /// Gets the highest UAP version of the current process.
        /// </summary>
        /// <returns>The highest UAP version of the current process.</returns>
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

        sealed class Comp : IAnimatedVisual, IDisposable
        {
            public Visual RootVisual { get; set; }

            public TimeSpan Duration { get; set; }

            public System.Numerics.Vector2 Size { get; set; }

            public void Dispose()
            {
                RootVisual?.Dispose();
            }
        }

        // ----
        // BEGIN: DEBUGGING HELPERS
        // ----

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

        // ----
        // END: DEBUGGING HELPERS
        // ----
    }
}