// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless
{
#if PUBLIC_IR
    public
#endif
    sealed class TreelessSolidLayer : TreelessLayer
    {
        public TreelessSolidLayer(
            in LayerArgs args,
            int width,
            int height,
            Color color)
            : base(in args)
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
