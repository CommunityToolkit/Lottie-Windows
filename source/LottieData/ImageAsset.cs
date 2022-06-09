// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
    /// <summary>
    /// An image.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    abstract class ImageAsset : Asset
    {
        public ImageAsset(string id, double width, double height)
            : base(id)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }

        public double Height { get; }

        /// <inheritdoc/>
        public override AssetType Type => AssetType.Image;

        public abstract ImageAssetType ImageType { get; }

        public enum ImageAssetType
        {
            Embedded,
            External,
        }
    }
}
