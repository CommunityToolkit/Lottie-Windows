// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
#if PUBLIC_IR
    public
#endif
    sealed class TreelessImageLayer : TreelessLayer
    {
        public TreelessImageLayer(
            BlendMode blendMode,
            bool is3d,
            MatteType matteType,
            IReadOnlyList<Mask> masks,
            ImageAsset imageAsset)
            : base(blendMode, is3d, matteType, masks)
        {
            ImageAsset = imageAsset;
        }

        public ImageAsset ImageAsset { get; }

        /// <inheritdoc/>
        public override TreelessLayerType Type => TreelessLayerType.Image;
    }
}
