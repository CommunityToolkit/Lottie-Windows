// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    /// <summary>
    /// Specifies the color space for interpolating color values in <see cref="ColorKeyFrameAnimation"/>.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    enum CompositionColorSpace
    {
        Auto = 0,
        Hsl = 1,
        Rgb = 2,
        HslLinear = 3,
        RgbLinear = 4,
    }
}
