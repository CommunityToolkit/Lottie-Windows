// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.ComponentModel;
using Windows.UI;

namespace LottieViewer
{
    // An observable named color with the ability to change the color value.
    public sealed class ColorPaletteEntry : INotifyPropertyChanged
    {
        Color _initialColor;
        Color _color;

        internal ColorPaletteEntry(Color color, string name)
        {
            _initialColor = color;
            _color = color;
            Name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// If true, changing the Color will also change the InitialColor to keep it the
        /// same as Color. This is used to special case the "Background" color, which does
        /// not have an initial color.
        /// </summary>
        public bool IsInitialColorSameAsColor { get; set; }

        public Color Color
        {
            get => _color;
            set
            {
                // Check whether the color is actually changing, and ignore it if it isn't.
                // This check ensures we can use two-way binding without infinite recursion.
                if (value != _color)
                {
                    _color = value;
                    if (IsInitialColorSameAsColor)
                    {
                        _initialColor = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.InitialColor)));
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Color)));
                }
            }
        }

        public Color InitialColor => _initialColor;

        // A name that describes the palette entry.
        public string Name { get; set; }
    }
}
