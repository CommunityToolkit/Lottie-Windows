// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.IO;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.DotLottie
{
    sealed class DotLottieFileAnimation
    {
        readonly DotLottieFile _owner;

        internal DotLottieFileAnimation(
            DotLottieFile owner,
            string id,
            bool loop)
        {
            _owner = owner;
            Id = id;
            Loop = loop;
        }

        public string Id { get; }

        public bool Loop { get; }

        public string Path => $"animations/{Id}.json";

        public Stream Open() => _owner.ZipArchive.GetEntry(Path).Open();
    }
}
