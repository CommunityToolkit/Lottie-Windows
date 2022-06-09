// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class Asset
    {
        private protected Asset(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public abstract AssetType Type { get; }

        public enum AssetType
        {
            LayerCollection,
            Image,
        }
    }
}
