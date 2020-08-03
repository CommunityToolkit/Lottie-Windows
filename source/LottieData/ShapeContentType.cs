// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Types of <see cref="ShapeLayerContent"/> objects.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    enum ShapeContentType
    {
        Ellipse,
        Group,
        LinearGradientFill,
        LinearGradientStroke,
        MergePaths,
        Path,
        Polystar,
        RadialGradientFill,
        RadialGradientStroke,
        Rectangle,
        Repeater,
        RoundCorners,
        SolidColorFill,
        SolidColorStroke,
        Transform,
        TrimPath,
    }
}
