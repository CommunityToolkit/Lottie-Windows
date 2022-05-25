// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace CommunityToolkit.WinUI.Lottie
{
    static class Uris
    {
        /// <summary>
        /// Parses a string into an absolute URI, or null if the string is malformed.
        /// Relative URIs are made relative to ms-appx:///.
        /// </summary>
        /// <returns>A Uri or null.</returns>
        public static Uri? StringToUri(string uri)
        {
            if (!Uri.IsWellFormedUriString(uri, UriKind.RelativeOrAbsolute))
            {
                return null;
            }

            return GetAbsoluteUri(new Uri(uri, UriKind.RelativeOrAbsolute));
        }

        /// <summary>
        /// Returns an absolute URI. Relative URIs are made relative to ms-appx:///.
        /// </summary>
        /// <returns>A Uri or null.</returns>
        [return: NotNullIfNotNull("uri")]
        public static Uri? GetAbsoluteUri(Uri uri)
        {
            if (uri is null)
            {
                return null;
            }

            if (uri.IsAbsoluteUri)
            {
                return uri;
            }

            return new Uri($"ms-appx:///{uri}", UriKind.Absolute);
        }
    }
}
