// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// The direction in which a path is to be drawn. This affects
    /// trims, and non-zero winding fills.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    enum DrawingDirection
    {
        Unknown = 0,

        /// <summary>
        /// The path is to be drawn in the forward direction. For rectangles and
        /// ellipses the forward direction is clockwise.
        /// </summary>
        Forward,

        /// <summary>
        /// The path is to be drawn in the reverse direction. For rectangles and
        /// ellipses the reverse direction is counter-clockwise.
        /// </summary>
        Reverse,
    }
}
