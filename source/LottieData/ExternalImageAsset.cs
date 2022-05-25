// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
    /// <summary>
    /// A reference to an image.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class ExternalImageAsset : ImageAsset
    {
        public ExternalImageAsset(string id, double width, double height, string path, string fileName)
            : base(id, width, height)
        {
            Path = path;
            FileName = fileName;
        }

        public string Path { get; }

        public string FileName { get; }

        /// <inheritdoc/>
        public override ImageAssetType ImageType => ImageAssetType.External;
    }
}
