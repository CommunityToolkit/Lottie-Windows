// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers
{
#if PUBLIC_IR
    public
#endif
    sealed class PreCompLayer : Layer
    {
        public PreCompLayer(
            in LayerArgs args,
            string refId,
            Vector2 size)
            : base(in args)
        {
            RefId = refId;
            Size = size;
        }

        /// <summary>
        /// Gets the id of an <see cref="Asset"/> that contains the <see cref="Layer"/>s under this <see cref="PreCompLayer"/>.
        /// </summary>
        public string RefId { get; }

        /// <summary>
        /// The size of the layer. <see cref="PreCompLayer"/>s clip to their size.
        /// </summary>
        public Vector2 Size { get; }

        /// <inheritdoc/>
        public override LayerType Type => LayerType.PreComp;
    }
}
