// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(5)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionGradientBrush : CompositionBrush
    {
        internal CompositionGradientBrush()
        {
        }

        // Default is Vector2.Zero.
        public Vector2? AnchorPoint { get; set; }

        // Default is Vector2.Zero.
        public Vector2? CenterPoint { get; set; }

        public IList<CompositionColorGradientStop> ColorStops { get; } = new List<CompositionColorGradientStop>();

        // Default is Clamp.
        public CompositionGradientExtendMode? ExtendMode { get; set; }

        // Default is RGB.
        public CompositionColorSpace? InterpolationSpace { get; set; }

        // Default is Relative.
        public CompositionMappingMode? MappingMode { get; set; }

        // Default is Vector2.Zero.
        public Vector2? Offset { get; set; }

        // Default is 0.
        public float? RotationAngleInDegrees { get; set; }

        // Default is Vector2.One.
        public Vector2? Scale { get; set; }

        // Default is Matrix3x2.Identity.
        public Matrix3x2? TransformMatrix { get; set; }
    }
}
