// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
    public
#endif
    sealed class Char
    {
        public Char(
              string? characters,
              string? fontFamily,
              string? style,
              double fontSize,
              double width,
              IEnumerable<ShapeLayerContent> shapes)
        {
            Characters = characters;
            FontFamily = fontFamily;
            Style = style;
            FontSize = fontSize;
            Width = Width;
            Shapes = shapes.ToArray();
        }

        public string? Characters { get; }

        public string? FontFamily { get; }

        public string? Style { get; }

        public double FontSize { get; }

        public double Width { get; }

        public IReadOnlyList<ShapeLayerContent> Shapes { get; }
    }
}
