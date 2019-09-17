// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public CompositionBrush FillBrush { get; set; }

        public CompositionGeometry Geometry { get; set; }

        public bool IsStrokeNonScaling { get; set; }

        public CompositionBrush StrokeBrush { get; set; }

        public CompositionStrokeCap StrokeDashCap { get; set; } = CompositionStrokeCap.Flat;

        public float StrokeDashOffset { get; set; }

        public List<float> StrokeDashArray { get; } = new List<float>();

        public CompositionStrokeCap StrokeEndCap { get; set; } = CompositionStrokeCap.Flat;

        public CompositionStrokeLineJoin StrokeLineJoin { get; set; } = CompositionStrokeLineJoin.Miter;

        public CompositionStrokeCap StrokeStartCap { get; set; } = CompositionStrokeCap.Flat;

        public float StrokeMiterLimit { get; set; } = 1;

        public float StrokeThickness { get; set; } = 1;

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionSpriteShape;
    }
}
