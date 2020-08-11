// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;
using Windows.UI;
using Windows.UI.Composition;
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
                _diagnosticsViewModel = value;
                if (_diagnosticsViewModel != null)
                {
                    // TODO - unhook these.
                    value.ThemePropertyBindings.CollectionChanged += Value_CollectionChanged;
                }
            }
        }

        public ObservableCollection<ColorPaletteEntry> PaletteEntries { get; } = new ObservableCollection<ColorPaletteEntry>();

        void PaletteEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems.Count == PaletteEntries.Count)
                    {
                        // All new items. Select the first.
                        _listBox.SelectedIndex = 0;
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    break;

                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Reset:
                default:
                    throw new InvalidOperationException();
            }
        }

        void Value_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
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
                    // Remove all except the first item in PaletteEntries (first item is background)
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
            if (_listBox.SelectedItem is ColorPaletteEntry selectedEntry)
            {
                selectedEntry.Color = args.NewColor;
            }
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
