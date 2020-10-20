// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionSpriteShape : CompositionShape
    {
        internal CompositionSpriteShape()
        {
        }

        public CompositionBrush? FillBrush { get; set; }

        public CompositionGeometry? Geometry { get; set; }

        public bool IsStrokeNonScaling { get; set; }

        public CompositionBrush? StrokeBrush { get; set; }

        public float StrokeDashOffset { get; set; }

        public List<float> StrokeDashArray { get; } = new List<float>();

        // Default is CompositionStrokeCap.Flat.
        public CompositionStrokeCap? StrokeDashCap { get; set; }

        // Default is CompositionStrokeCap.Flat.
        public CompositionStrokeCap? StrokeStartCap { get; set; }

        // Default is CompositionStrokeCap.Flat.
        public CompositionStrokeCap? StrokeEndCap { get; set; }

        // Default is CompositionStrokeLineJoin.Miter.
        public CompositionStrokeLineJoin? StrokeLineJoin { get; set; }

        // Default is 1.
        public float? StrokeMiterLimit { get; set; }

        // Default is 1.
        public float? StrokeThickness { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionSpriteShape;
    }
}
