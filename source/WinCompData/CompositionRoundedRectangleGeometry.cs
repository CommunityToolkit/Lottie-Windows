// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class CompositionRoundedRectangleGeometry : CompositionGeometry
    {
        public Vector2 CornerRadius { get; set; }

        public Vector2? Offset { get; set; }

        public Vector2 Size { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionRoundedRectangleGeometry;
    }
}
