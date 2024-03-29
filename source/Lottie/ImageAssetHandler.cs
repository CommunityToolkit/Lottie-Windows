// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

#if WINAPPSDK
using Microsoft.UI.Composition;
#else
using Windows.UI.Composition;
#endif

namespace CommunityToolkit.WinUI.Lottie
{
    /// <summary>
    /// A delegate that returns an <see cref="ICompositionSurface"/> for the given image uri.
    /// </summary>
    /// <returns>A surface for the image referenced by <paramref name="imageUri"/>
    /// or null.</returns>
    /// <remarks>Users can provide an <see cref="ImageAssetHandler"/> in order to
    /// provide a bitmap for an image referenced in a Lottie file. </remarks>
    public delegate ICompositionSurface? ImageAssetHandler(Uri imageUri);
}
