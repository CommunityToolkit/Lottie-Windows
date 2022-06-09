// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(8)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionRadialGradientBrush : CompositionGradientBrush
    {
        internal CompositionRadialGradientBrush()
        {
        }

        // Default is 0.5, 0.5
        public Vector2? EllipseCenter { get; set; }

        // Default is 0.5, 0.5
        public Vector2? EllipseRadius { get; set; }

        // Default is 0, 0
        public Vector2? GradientOriginOffset { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionRadialGradientBrush;
    }
}
