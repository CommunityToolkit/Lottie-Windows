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

        public Vector2? AnchorPoint { get; set; }

        public Vector2? CenterPoint { get; set; }

        public IList<CompositionColorGradientStop> ColorStops { get; } = new List<CompositionColorGradientStop>();

        public CompositionGradientExtendMode? ExtendMode { get; set; }

        public CompositionColorSpace? InterpolationSpace { get; set; }

        // Default is Relative
        public CompositionMappingMode? MappingMode { get; set; }

        public Vector2? Offset { get; set; }

        public float? RotationAngleInDegrees { get; set; }

        public Vector2? Scale { get; set; }

        public Matrix3x2? TransformMatrix { get; set; }
    }
}
