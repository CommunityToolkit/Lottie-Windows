// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A reference to an image.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class ImageAsset : Asset
    {
        public ImageAsset(string id, double width, double height, string path, string fileName)
            : base(id)
        {
            Width = width;
            Height = height;
            Path = path;
            FileName = fileName;
        }

        public double Width { get; }

        public double Height { get; }

        public string Path { get; }

        public string FileName { get; }

        /// <inheritdoc/>
        public override AssetType Type => AssetType.Image;
    }
}
