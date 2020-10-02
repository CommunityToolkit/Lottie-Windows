// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using LottieViewer.ViewModel;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace LottieViewer
{
    /// <summary>
    /// Displays a color picker for multiple colors in a palette.
    /// </summary>
    public sealed partial class PaletteColorPicker : UserControl
    {
        LottieVisualDiagnosticsViewModel _diagnosticsViewModel;

        // Used to prevent infinite recursion when the color picker is updated.
        // Needed because we have 2-way binding between 2 color pickers and they
        // try to set each others values.
        bool m_isColorPickerChanging = false;

        public PaletteColorPicker()
        {
            this.InitializeComponent();
            PaletteEntries.CollectionChanged += PaletteEntries_CollectionChanged;
        }

        internal LottieVisualDiagnosticsViewModel DiagnosticsViewModel
        {
            get => _diagnosticsViewModel;
            set
            {
                if (_diagnosticsViewModel != null)
                {
                    // Unhook form the previous DiagnosticsViewModel.
                    value.ThemePropertyBindings.CollectionChanged -= Value_CollectionChanged;
                }

                _diagnosticsViewModel = value;

                if (_diagnosticsViewModel != null)
                {
                    value.ThemePropertyBindings.CollectionChanged += Value_CollectionChanged;
                }
            }
        }

        public ObservableCollection<ColorPaletteEntry> PaletteEntries { get; } = new ObservableCollection<ColorPaletteEntry>();

        void PaletteEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                // These are the only cases we expect becasue of the way we modify the collection.
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                    break;

                // These are never expected because of the way we modify the collection.
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Reset:
                default:
                    throw new InvalidOperationException();
            }

            // Ensure something is selected if there are any items in the list.
            if ((_listBox.SelectedIndex == -1 || _listBox.SelectedIndex >= PaletteEntries.Count)
                && PaletteEntries.Count > 0)
            {
                _listBox.SelectedIndex = PaletteEntries.Count - 1;
            }
        }

        void Value_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // Add the entry to the list, and hook it up so that changing the entry will update
                    // the entry in the theming property set.
                    foreach (PropertyBinding item in e.NewItems)
                    {
                        if (item.ExposedType == PropertySetValueType.Color)
                        {
                            var color = (Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui.Color)item.DefaultValue;
                            var entry = new ColorPaletteEntry(Color.FromArgb(color.A, color.R, color.G, color.B), item.DisplayName);
                            PaletteEntries.Add(entry);
                            entry.PropertyChanged += (_, args) =>
                            {
                                var newColor = entry.Color;
                                _diagnosticsViewModel.ThemingPropertySet.InsertVector4(item.BindingName, ColorAsVector4(entry.Color));
                            };
                        }
                    }

                    break;
                case NotifyCollectionChangedAction.Reset:
                    // Remove all except the first item in PaletteEntries (first item is Background).
                    while (PaletteEntries.Count > 1)
                    {
                        PaletteEntries.Remove(PaletteEntries[PaletteEntries.Count - 1]);
                    }

                    break;

                // These are all unexpected. Don't try to handle them.
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Move:
                default:
                    throw new InvalidOperationException();
            }
        }

        // Synchronizes the color picker's color with the selected item in the list.
        void PaletteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_listBox.SelectedItem is ColorPaletteEntry selectedEntry)
            {
                MyColorPicker.Color = selectedEntry.Color;
                MyColorPicker.IsEnabled = true;
            }
            else
            {
                MyColorPicker.Color = Color.FromArgb(0, 0, 0, 0);
                MyColorPicker.IsEnabled = false;
            }
        }

        void MyColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (m_isColorPickerChanging)
            {
                // Ignore if we're in the middle of changing the color already.
                return;
            }

            if (_listBox.SelectedItem is ColorPaletteEntry selectedEntry)
            {
                m_isColorPickerChanging = true;
                selectedEntry.Color = args.NewColor;
                TextColorPicker.Color = args.NewColor;
                m_isColorPickerChanging = false;
            }
        }

        void TextColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            // Update the main color picker.
            MyColorPicker.Color = args.NewColor;
        }

        // Handle double-click on an entry. Restore the original color.
        void PaletteListBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (GetDataContext((DependencyObject)e.OriginalSource) is ColorPaletteEntry colorPaletteEntry)
            {
                // Reset the color to the original color.
                colorPaletteEntry.Color = colorPaletteEntry.InitialColor;
                MyColorPicker.Color = colorPaletteEntry.Color;
            }

            // Search up the tree for an object with a data context, and returns
            // the data context.
            object GetDataContext(DependencyObject obj)
            {
                if (obj is FrameworkElement fe && fe.DataContext != null)
                {
                    return fe.DataContext;
                }
                else
                {
                    return obj is null ? null : GetDataContext(VisualTreeHelper.GetParent(obj));
                }
            }
        }

        // Converts a color to the Vector4 representation used in a CompositionPropertySet for
        // color binding.
        static Vector4 ColorAsVector4(Color color) => new Vector4(color.R, color.G, color.B, color.A);
    }
}
