// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class PreCompLayer : Layer
    {
        public PreCompLayer(
            in LayerArgs args,
            string refId,
            double width,
            double height)
            : base(in args)
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
    }
}
