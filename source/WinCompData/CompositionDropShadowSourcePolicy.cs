// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    /// <summary>
    /// The masking policy for a <see cref="DropShadow"/>.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    enum CompositionDropShadowSourcePolicy
    {
        Default = 0,
        InheritFromVisualContent = 1,
    }
}
