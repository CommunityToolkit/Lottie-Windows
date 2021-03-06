// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

//#define DebugDragDrop
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LottieViewer.ViewModel;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace LottieViewer
{
    /// <summary>
    /// MainPage.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        readonly ToggleButton[] _controlPanelButtons;
        int _playControlToggleVersion;
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

            // Remove all of the control panel panes. They will be added back as needed.
            ControlPanel.Children.Clear();
        }

        public ObservableCollection<object> PropertiesList { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> MarkersList { get; } = new ObservableCollection<object>();

        public string AppVersion
        {
            get
            {
                var version = Package.Current.Id.Version;

                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            }
        }

        public string UapVersion
        {
            get
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
                return (versionToTest - 1).ToString();
            }
        }

        void DiagnosticsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var list = PropertiesList;
            var viewModel = _stage.DiagnosticsViewModel;

            if (viewModel is null)
            {
                list.Clear();
                MarkersList.Clear();
            }
            else if (e.PropertyName == nameof(viewModel.FileName))
            {
                list.Clear();
                MarkersList.Clear();
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
                    list.Add(new PairOfStrings("Frames", $"{viewModel.FrameCountText} @ {viewModel.FramesPerSecond:0.#}fps"));

                    if (viewModel.Markers.Count > 0)
                    {
                        foreach (var marker in viewModel.Markers)
                        {
                            MarkersList.Add(marker);
                        }
                    }
                }
            }
        }

        internal ColorPaletteEntry BackgroundColor { get; } = new ColorPaletteEntry(Colors.White, "Background") { IsInitialColorSameAsColor = true };

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
                filePicker.FileTypeFilter.Add(".lottie");

                StorageFile? file = null;
                try
                {
                    file = await filePicker.PickSingleFileAsync();
                }
                catch
                {
                    // Ignore PickSingleFileAsync exceptions so they don't crash the process.
                }

                if (file is null)
                {
                    // User declined to pick anything.
                    return;
                }

                if (playVersion != _playVersion)
                {
                    return;
                }

                // Reset the scrubber to the 0 position.
                _scrubber.Value = 0;

                if (await _stage.TryLoadFileAsync(file))
                {
                    // Loading succeeded, start playing.
                    _playStopButton.IsChecked = true;
                }
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

                    var filteredItems = items.Where(IsJsonOrLottieFile);

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

            IStorageItem? item = null;
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

            if (item is null)
            {
                DebugDragDrop("Ignoring drop");
                return;
            }

            // Reset the scrubber to the 0 position.
            _scrubber.Value = 0;

            DebugDragDrop("Doing drop");
            if (await _stage.TryLoadFileAsync((StorageFile)item))
            {
                // Loading succeeded, start playing.
                _playStopButton.IsChecked = true;
            }
        }

        void LottieDragLeaveHandler(object sender, DragEventArgs e)
        {
            _stage.DoDragLeave();
        }

        [Conditional("DebugDragDrop")]
        static void DebugDragDrop(string text) => Debug.WriteLine(text);

        static bool IsJsonOrLottieFile(IStorageItem item) =>
            item.IsOfType(StorageItemTypes.File) &&
            (item.Name.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase) ||
            item.Name.EndsWith(".lottie", StringComparison.InvariantCultureIgnoreCase));

        bool _ignoreScrubberValueChanges;

        public event PropertyChangedEventHandler? PropertyChanged;

        void ProgressSliderChanged(object sender, ScrubberValueChangedEventArgs e)
        {
            if (!_ignoreScrubberValueChanges)
            {
                UncheckPlayStopButton();
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
            if (!_playStopButton.IsChecked)
            {
                _stage.Player.SetProgress(_scrubber.Value);
            }
            else
            {
                _ignoreScrubberValueChanges = true;
                _scrubber.Value = 0;
                _ignoreScrubberValueChanges = false;

                // If we were stopped in manual play control, turn it back to automatic.
                if (!_playStopButton.IsChecked)
                {
                    _playStopButton.IsChecked = true;
                }

                var playControlToggleVersion = ++_playControlToggleVersion;

                try
                {
                    await _stage.Player.PlayAsync(0, 1, looped: true);
                }
                catch
                {
                    // Ignore PlayAsync exceptions so they don't crash the process.
                }

                // Playing has finished. Make sure the PlayStopButton is no longer
                // checked, unless a newer play has started.
                if (playControlToggleVersion == _playControlToggleVersion)
                {
                    _playStopButton.IsChecked = false;
                }
            }
        }

        void UncheckPlayStopButton()
        {
            _playStopButton.IsChecked = false;
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

        public bool IsControlPanelVisible => _controlPanelButtons.Any(b => b.IsChecked == true);

        // Uncheck all the other control panel buttons when one is checked.
        // This allows toggle buttons to act like radio buttons.
        void ControlPanelButtonChecked(object sender, RoutedEventArgs e)
        {
            // Uncheck all the other buttons.
            foreach (var button in _controlPanelButtons)
            {
                if (button != sender)
                {
                    if (button.IsChecked == true)
                    {
                        button.IsChecked = false;
                    }
                }
            }

            // Remove all the children from the control pane Grid, then add back the
            // one that is is being shown. This is done to trigger the PaneThemeTransition
            // so that the pane slides in and out.
            ControlPanel.Children.Clear();

            // Add back the panel corresponding to the button that is checked.
            if (sender == InfoButton)
            {
                ControlPanel.Children.Add(InfoPanel);
            }
            else if (sender == PaletteButton)
            {
                ControlPanel.Children.Add(ColorPanel);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsControlPanelVisible)));
        }

        // When one of the control panel buttons is unchecked, if all the buttons
        // are now unpressed, remove the filler from the play/stop control bar so that
        // the scrubber takes up the whole area.
        void ControlPanelButtonUnchecked(object sender, RoutedEventArgs e)
        {
            // Remove all the children from the control pane Grid. This is done to
            // trigger the PaneThemeTransition so that the pane slides in and out.
            ControlPanel.Children.Clear();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsControlPanelVisible)));
        }

        // Called when the user clicks on a marker hyperlink.
        void MarkerClick(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var dataContext = ((FrameworkElement)sender.ElementStart.Parent).DataContext;
            var marker = (Marker)dataContext;
            SeekToProgressValue(marker.ConstrainedInProgress);
        }

        // Called when the user clicks on a marker-with-duration hyperlink.
        void MarkerEndClick(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var dataContext = ((FrameworkElement)sender.ElementStart.Parent).DataContext;
            var marker = (MarkerWithDuration)dataContext;
            SeekToProgressValue(marker.ConstrainedOutProgress);
        }

        // Sets the progress to the given value, and sets the focus to the scrubber
        // so that the arrow keys will control the position of the scrubber.
        void SeekToProgressValue(double progress)
        {
            UncheckPlayStopButton();

            // Set focus to the scrubber so that the arrow keys will move the
            // position of the scrubber.
            _scrubber.Focus(FocusState.Programmatic);
            _scrubber.Value = progress;
        }

        void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // By default, when the pointer is pressed, focus on the scrubber so
            // that the arrow keys can move the scrubber.
            e.Handled = true;
            _scrubber.Focus(FocusState.Pointer);
        }
    }
}
