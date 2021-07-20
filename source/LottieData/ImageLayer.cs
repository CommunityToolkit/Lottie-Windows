// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class ImageLayer : Layer
    {
        public ImageLayer(
            in LayerArgs args,
            string refId)
            : base(in args)
        {
            RefId = refId;
        }

        /// <summary>
        /// Gets the id of an <see cref="Asset"/> referenced by this layer.
        /// </summary>
        public string RefId { get; }

        /// <inheritdoc/>
        public override LayerType Type => LayerType.Image;

        public override Layer CopyAndChangeIndices(int index, int? parentIndex)
        {
            return new ImageLayer(CopyArgsAndChangeIndices(index, parentIndex), RefId);
        }
    }
}
