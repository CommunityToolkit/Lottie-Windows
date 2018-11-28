// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class SolidLayer : Layer
    {
        public SolidLayer(
            string name,
            int index,
            int? parent,
            bool isHidden,
            Transform transform,
            int width,
            int height,
            Color color,
            double timeStretch,
            double startFrame,
            double inFrame,
            double outFrame,
            BlendMode blendMode,
            bool is3d,
            bool autoOrient,
            IEnumerable<Mask> masks)
            : base(
             name,
             index,
             parent,
             isHidden,
             transform,
             timeStretch,
             startFrame,
             inFrame,
             outFrame,
             blendMode,
             is3d,
             autoOrient,
             masks)
        {
            Color = color;
            Height = height;
            Width = width;
        }

        public Color Color { get; }

        public int Height { get; }

        public int Width { get; }

        /// <inheritdoc/>
        public override LayerType Type => LayerType.Solid;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.SolidLayer;
    }
}
