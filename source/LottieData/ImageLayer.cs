// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class ImageLayer : Layer
    {
        public ImageLayer(
            string name,
            int index,
            int? parent,
            bool isHidden,
            Transform transform,
            double timeStretch,
            double startFrame,
            double inFrame,
            double outFrame,
            BlendMode blendMode,
            bool is3d,
            bool autoOrient,
            string refId,
            IEnumerable<Mask> masks,
            MatteType layerMatteType)
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
                 masks,
                 layerMatteType)
        {
            RefId = refId;
        }

        /// <summary>
        /// Gets the id of an <see cref="Asset"/> referenced by this layer.
        /// </summary>
        public string RefId { get; }

        /// <inheritdoc/>
        public override LayerType Type => LayerType.Image;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.ImageLayer;
    }
}
