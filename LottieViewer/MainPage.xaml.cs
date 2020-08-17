// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//#define DebugDragDrop
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LottieViewer.ViewModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1402 // File may only contain a single type

namespace LottieViewer
{
    /// <summary>
    /// MainPage.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        readonly ToggleButton[] _controlPanelButtons;
        int _playVersion;

        public MainPage()
        {
            InitializeComponent();

            // The control panel buttons. We hold onto these in order to ensure that no more
            // than one is checked at the same time.
            _controlPanelButtons = new[] { PaletteButton, InfoButton };

            // Connect the player's progress to the scrubber's progress.
            _scrubber.SetAnimatedCompositionObject(_stage.Player.ProgressObject);

            // Add the background to the color picker so that it can be modified by the user.
            _paletteColorPicker.PaletteEntries.Add(BackgroundColor);

            // Get notified when info about the loaded Lottie changes.
            _stage.DiagnosticsViewModel.PropertyChanged += DiagnosticsViewModel_PropertyChanged;
        }

        public ObservableCollection<object> PropertiesList { get; } = new ObservableCollection<object>();

        void DiagnosticsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var list = PropertiesList;
            var viewModel = _stage.DiagnosticsViewModel;

            if (viewModel is null)
            {
                list.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.FileName))
            {
                list.Clear();
                if (!string.IsNullOrWhiteSpace(viewModel.FileName))
                {
                    list.Add(new PairOfStrings("File", viewModel.FileName));
                }

                // If the Lottie has 0 duration then it isn't valid, so don't show properties
                // the only make sense for valid Lotties.
                if (viewModel.LottieVisualDiagnostics?.Duration.Ticks > 0)
                {
                    // Not all Lotties have a name, so only add the name if it exists.
                    if (!string.IsNullOrWhiteSpace(viewModel.Name))
                    {
                        list.Add(new PairOfStrings("Name", viewModel.Name));
                    }

                    list.Add(new PairOfStrings("Size", viewModel.SizeText));
                    list.Add(new PairOfStrings("Duration", viewModel.DurationText));

                    foreach (var marker in viewModel.Markers)
                    {
                        list.Add(marker);
                    }
                }
            }
        }

        internal ColorPaletteEntry BackgroundColor { get; } = new ColorPaletteEntry(Colors.White, "Background");

        void PickFile_Click(object sender, RoutedEventArgs e)
            => _ = OnPickFileAsync();

        async Task OnPickFileAsync()
        {
            try
            {
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
            finally
            {
                // Uncheck the button. The button is a ToggleButton so that it indicates
                // visually when the file picker is open. We need to manually reset its state.
                PickFile.IsChecked = false;
            }
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

        public event PropertyChangedEventHandler PropertyChanged;

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
            var issues = _stage.DiagnosticsViewModel.Issues;
            var dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(string.Join("\r\n", issues.Select(iss => iss.ToString())));
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }

        // Uncheck all the other control panel buttons when one is checked.
        // This allows toggle buttons to act like radio buttons.
        void ControlPanelButtonChecked(object sender, RoutedEventArgs e)
        {
            foreach (var button in _controlPanelButtons)
            {
                if (button != sender)
                {
                    button.IsChecked = false;
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsControlPanelVisible)));
        }

        public bool IsControlPanelVisible => _controlPanelButtons.Any(b => b.IsChecked == true);

        // When one of the control panel buttons is unchecked, if all the buttons
        // are now unpressed, remove the filler from the play/stop control bar so that
        // the scrubber takes up the whole area.
        void ControlPanelButtonUnchecked(object sender, RoutedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsControlPanelVisible)));
        }

        // Called when the user clicks on a marker hyperlink.
        void MarkerClick(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            var dataContext = ((FrameworkElement)sender.ElementStart.Parent).DataContext;
            var marker = (Marker)dataContext;

            // Ensure the Play button is unchecked because SetProgress will stop playing.
            _playStopButton.IsChecked = false;

            // Set the progress to the marker value.
            _stage.Player.SetProgress(marker.Progress);
        }

        // Called when the user clicks on a marker-with-duration hyperlink.
        void MarkerEndClick(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            var dataContext = ((FrameworkElement)sender.ElementStart.Parent).DataContext;
            var marker = (MarkerWithDuration)dataContext;

            // Ensure the Play button is unchecked because SetProgress will stop playing.
            _playStopButton.IsChecked = false;

            // Set the progress to the marker value.
            _stage.Player.SetProgress(marker.ToProgress);
        }
    }

    public sealed class VisiblityConverter : IValueConverter
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

    public sealed class FloatFormatter : IValueConverter
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

    public sealed class PropertiesTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Normal { get; set; }

        public DataTemplate Marker { get; set; }

        public DataTemplate MarkerWithDuration { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is PairOfStrings)
            {
                return Normal;
            }
            else if (item is Marker)
            {
                return Marker;
            }
            else
            {
                return MarkerWithDuration;
            }
        }
    }
}
