// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
    /// <summary>
    /// An image embedded in the Lottie file.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class EmbeddedImageAsset : ImageAsset
    {
        public EmbeddedImageAsset(string id, double width, double height, byte[] bytes, string format)
            : base(id, width, height)
        {
            Bytes = bytes;
            Format = format;
        }

        /// <summary>
        /// The data of the image. The interpretation of the data depends on the <see cref="Format"/>.
        /// </summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// The format of the <see cref="Bytes"/>. Typically this is jpg or png.
        /// </summary>
        public string Format { get; }

        /// <inheritdoc/>
        public override ImageAssetType ImageType => ImageAssetType.Embedded;
    }
}
