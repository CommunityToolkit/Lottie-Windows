// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Windows.UI;

namespace LottieViewer
{
    // An observable named color with the ability to change the color value.
    public sealed class ColorPaletteEntry : INotifyPropertyChanged
    {
        Color _color;

        internal ColorPaletteEntry(Color color, string name)
        {
            InitialColor = color;
            _color = color;
            Name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Color InitialColor { get; }

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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Color)));
                }
            }
        }

        // A name that describes the palette entry.
        public string Name { get; set; }
    }
}
