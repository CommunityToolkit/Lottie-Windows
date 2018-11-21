// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    abstract class Asset
    {
        protected private Asset(string id)
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
