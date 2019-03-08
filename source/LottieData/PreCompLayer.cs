// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class PreCompLayer : Layer
    {
        public PreCompLayer(
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
            double width,
            double height,
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
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the id of an <see cref="Asset"/> that contains the <see cref="Layer"/>s under this <see cref="PreCompLayer"/>.
        /// </summary>
        public string RefId { get; }

        public double Width { get; }

        public double Height { get; }

        /// <inheritdoc/>
        public override LayerType Type => LayerType.PreComp;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.PreCompLayer;
    }
}
