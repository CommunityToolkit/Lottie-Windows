// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers
{
    /// <summary>
    /// A <see cref="LayerCollection"/> stored in the assets section of a <see cref="IRComposition"/>.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    sealed class LayerCollectionAsset : Asset
    {
        public LayerCollectionAsset(string id, LayerCollection layers)
            : base(id)
        {
            Layers = layers;
        }

        public LayerCollection Layers { get; }

        /// <inheritdoc/>
        public override AssetType Type => AssetType.LayerCollection;
    }
}
