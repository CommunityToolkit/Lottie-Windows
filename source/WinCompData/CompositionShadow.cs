// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    /// <summary>
    /// Data representation of Windows.UI.Composition.CompositionShadow.
    /// </summary>
    [MetaData.UapVersion(3)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionShadow : CompositionObject
    {
        private protected CompositionShadow()
        {
        }
    }
}
