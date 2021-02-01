// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.IO;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.DotLottie
{
    /// <summary>
    /// Describes an animation in a .lottie file. Animations
    /// are Lottie .json files.
    /// </summary>
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

        /// <summary>
        /// The path to the animation in the .lottie archive.
        /// </summary>
        public string Path => $"/animations/{Id}.json";

        public Stream Open()
        {
            // Eliminate the leading "/"
            var path = Path.Substring(1);
            var entry = _owner.ZipArchive.GetEntry(path);
            if (entry is null)
            {
                // The manifest said that the entry would be there, but
                // it's not.
                throw new InvalidLottieFileException();
            }

            return entry.Open();
        }
    }
}
