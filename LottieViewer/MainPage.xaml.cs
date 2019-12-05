// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//#define DebugDragDrop
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace LottieViewer
{
    /// <summary>
    /// MainPage.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        int _playVersion;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public MainPage()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            InitializeComponent();

            // Connect the player's progress to the scrubber's progress.
            _scrubber.SetAnimatedCompositionObject(_stage.Player.ProgressObject);
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100
        async void PickFile_Click(object sender, RoutedEventArgs e)
        {
#pragma warning restore VSTHRD100
            var playVersion = ++_playVersion;

            var filePicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
            };
            filePicker.FileTypeFilter.Add(".json");

            StorageFile file = null;
            try
            {
                file = await filePicker.PickSingleFileAsync();
            }
            catch
            {
                // Ignore PickSingleFileAsync exceptions so they don't crash the process.
            }

            if (file == null)
            {
                // Used declined to pick anything.
                return;
            }

            if (playVersion != _playVersion)
            {
                return;
            }

            // Reset the scrubber to the 0 position.
            _scrubber.Value = 0;

            // If we were stopped in manual play control, turn it back to automatic.
            if (!_playStopButton.IsChecked.Value)
            {
                _playStopButton.IsChecked = true;
            }

            _stage.DoDragDropped(file);
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100
        async void LottieDragEnterHandler(object sender, DragEventArgs e)
        {
#pragma warning restore VSTHRD100
            DebugDragDrop("Drag enter");

            // Only accept files.
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                // Get a deferral to keep the drag operation alive until the async
                // methods have completed.
                var deferral = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();

                    var filteredItems = items.Where(IsJsonFile);

                    if (!filteredItems.Any() || filteredItems.Skip(1).Any())
                    {
                        DebugDragDrop("Drag enter - ignoring");
                        return;
                    }

                    // Exactly one item was selected.
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.DragUIOverride.Caption = "Drop to view Lottie.";
                }
                catch
                {
                    // Ignore async exception so they don't crash the process.
                }
                finally
                {
                    DebugDragDrop("Completing drag deferral");
                    deferral.Complete();
                }

                DebugDragDrop("Doing drag enter");
                _stage.DoDragEnter();
            }
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100

        // Called when an item is dropped.
        async void LottieDropHandler(object sender, DragEventArgs e)
        {
#pragma warning restore VSTHRD100
            DebugDragDrop("Dropping");
            var playVersion = ++_playVersion;

            IStorageItem item = null;
            try
            {
                item = (await e.DataView.GetStorageItemsAsync()).Single();
            }
            catch
            {
                // Ignore GetStorageItemsAsync exceptions so they don't crash the process.
            }

            if (playVersion != _playVersion)
            {
                DebugDragDrop("Ignoring drop");
                return;
            }

            // Reset the scrubber to the 0 position.
            _scrubber.Value = 0;

            // If we were stopped in manual play control, turn it back to automatic.
            if (!_playStopButton.IsChecked.Value)
            {
                _playStopButton.IsChecked = true;
            }

            DebugDragDrop("Doing drop");
            _stage.DoDragDropped((StorageFile)item);
        }

        void LottieDragLeaveHandler(object sender, DragEventArgs e)
        {
            _stage.DoDragLeave();
        }

        [Conditional("DebugDragDrop")]
        static void DebugDragDrop(string text) => Debug.WriteLine(text);

        static bool IsJsonFile(IStorageItem item) => item.IsOfType(StorageItemTypes.File) && item.Name.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase);

        bool _ignoreScrubberValueChanges;

        void ProgressSliderChanged(object sender, ScrubberValueChangedEventArgs e)
        {
            if (!_ignoreScrubberValueChanges)
            {
                _playStopButton.IsChecked = false;
                _stage.Player.SetProgress(e.NewValue);
            }
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100
#pragma warning disable SA1300 // Element should begin with upper-case letter
        async void _playControl_Toggled(object sender, RoutedEventArgs e)
#pragma warning restore SA1300 // Element should begin with upper-case letter
        {
#pragma warning restore VSTHRD100
            // If no Lottie is loaded, do nothing.
            if (!_stage.Player.IsAnimatedVisualLoaded)
            {
                return;
            }

            // Otherwise, if we toggled on, we're stopped in manual mode: set the progress.
            //            If we toggled off, we're in auto mode, start playing.
            if (!_playStopButton.IsChecked.Value)
            {
                _stage.Player.SetProgress(_scrubber.Value);
            }
            else
            {
                _ignoreScrubberValueChanges = true;
                _scrubber.Value = 0;
                _ignoreScrubberValueChanges = false;

                // If we were stopped in manual play control, turn it back to automatic.
                if (!_playStopButton.IsChecked.Value)
                {
                    _playStopButton.IsChecked = true;
                }

                try
                {
                    await _stage.Player.PlayAsync(0, 1, looped: true);
                }
                catch
                {
                    // Ignore PlayAsync exceptions so they don't crash the process.
                }
            }
        }

        void CopyIssuesToClipboard(object sender, RoutedEventArgs e)
        {
            var issues = _stage.PlayerIssues;
            var dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(string.Join("\r\n", issues.Select(iss => iss.ToString())));
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class VisiblityConverter : IValueConverter
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore SA1402 // File may only contain a single type
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                if ((string)parameter == "not")
                {
                    boolValue = !boolValue;
                }

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return null;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Only support one way binding.
            throw new NotImplementedException();
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class FloatFormatter : IValueConverter
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore SA1402 // File may only contain a single type
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, string language)
        {
            return ((double)value).ToString("0.#");
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Only support one way binding.
            throw new NotImplementedException();
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class LottieVisualDiagnosticsFormatter : IValueConverter
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore SA1402 // File may only contain a single type
    {
        static string MSecs(TimeSpan timeSpan) => $"{timeSpan.TotalMilliseconds.ToString("#,##0.0")} mSecs";

        object IValueConverter.Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter as string == "CollapsedIfNull" && targetType == typeof(Visibility))
            {
                return value == null ? Visibility.Collapsed : Visibility.Visible;
            }

            var diagnostics = value as LottieVisualDiagnostics;

            switch (parameter as string)
            {
                case "Properties":
                    if (diagnostics == null) { return null; }
                    return DiagnosticsToProperties(diagnostics).ToArray();
                case "Issues":
                    {
                        if (diagnostics == null) { return null; }
                        var allIssues = diagnostics.JsonParsingIssues.Select(iss => iss.Description).Concat(diagnostics.TranslationIssues.Select(iss => iss.Description));
                        if (targetType == typeof(Visibility))
                        {
                            return allIssues.Any() ? Visibility.Visible : Visibility.Collapsed;
                        }
                        else
                        {
                            return allIssues.OrderBy(a => a);
                        }
                    }

                case "ParsingIssues":
                    if (diagnostics == null) { return null; }
                    if (targetType == typeof(Visibility))
                    {
                        return diagnostics.JsonParsingIssues.Any() ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        return diagnostics.JsonParsingIssues.OrderBy(a => a);
                    }

                case "TranslationIssues":
                    if (diagnostics == null) { return null; }
                    if (targetType == typeof(Visibility))
                    {
                        return diagnostics.TranslationIssues.Any() ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        return diagnostics.TranslationIssues.OrderBy(a => a);
                    }

                case "VisibleIfIssues":
                    if (diagnostics == null)
                    {
                        return Visibility.Collapsed;
                    }

                    return diagnostics.JsonParsingIssues.Any() || diagnostics.TranslationIssues.Any() ? Visibility.Visible : Visibility.Collapsed;
                default:
                    break;
            }

            return null;
        }

        IEnumerable<Tuple<string, string>> DiagnosticsToProperties(LottieVisualDiagnostics diagnostics)
        {
            yield return Tuple.Create("File name", diagnostics.FileName);
            yield return Tuple.Create("Duration", $"{diagnostics.Duration.TotalSeconds.ToString("#,##0.0##")} secs");
            var aspectRatio = FloatToRatio(diagnostics.LottieWidth / diagnostics.LottieHeight);
            yield return Tuple.Create("Aspect ratio", $"{aspectRatio.Item1.ToString("0.###")}:{aspectRatio.Item2.ToString("0.###")}");
            yield return Tuple.Create("Size", $"{diagnostics.LottieWidth} x {diagnostics.LottieHeight}");

            foreach (var marker in diagnostics.Markers)
            {
                yield return Tuple.Create("Marker", $"{marker.Key}: {marker.Value.ToString("0.0###")}");
            }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Only support one way binding.
            throw new NotImplementedException();
        }

        // Returns a pleasantly simplified ratio for the given value.
        static (double, double) FloatToRatio(double value)
        {
            const int maxRatioProduct = 200;
            var candidateN = 1.0;
            var candidateD = Math.Round(1 / value);
            var error = Math.Abs(value - (candidateN / candidateD));

            for (double n = candidateN, d = candidateD; n * d <= maxRatioProduct && error != 0;)
            {
                if (value > n / d)
                {
                    n++;
                }
                else
                {
                    d++;
                }

                var newError = Math.Abs(value - (n / d));
                if (newError < error)
                {
                    error = newError;
                    candidateN = n;
                    candidateD = d;
                }
            }

            // If we gave up because the numerator or denominator got too big then
            // the number is an approximation that requires some decimal places.
            // Get the real ratio by adjusting the denominator or numerator - whichever
            // requires the smallest adjustment.
            if (error != 0)
            {
                if (value > candidateN / candidateD)
                {
                    candidateN = candidateD * value;
                }
                else
                {
                    candidateD = candidateN / value;
                }
            }

            return (candidateN, candidateD);
        }
    }
}
