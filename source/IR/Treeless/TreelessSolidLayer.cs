// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
#if PUBLIC_IR
    public
#endif
    sealed class TreelessSolidLayer : TreelessLayer
    {
        public TreelessSolidLayer(
            BlendMode blendMode,
            bool is3d,
            MatteType matteType,
            IReadOnlyList<Mask> masks,
            int width,
            int height,
            Color color)
           : base(blendMode, is3d, matteType, masks)
        {
            Color = color;
            Height = height;
            Width = width;
        }

        public Color Color { get; }

        public int Height { get; }

        public int Width { get; }

        /// <inheritdoc/>
        public override TreelessLayerType Type => TreelessLayerType.Solid;
    }
}
