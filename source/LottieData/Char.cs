// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
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
        }
    }
}
