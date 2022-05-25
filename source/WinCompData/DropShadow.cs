// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Numerics;
using CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    /// <summary>
    /// Data representation of Windows.UI.Composition.CompositionShadow.
    /// </summary>
    [MetaData.UapVersion(3)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class DropShadow : CompositionShadow
    {
        internal DropShadow()
        {
        }

        // Defaults to 9.0F
        public float? BlurRadius { get; set; }

        // Defaults to black.
        public Color? Color { get; set; }

        // Opacity mask.
        public CompositionBrush? Mask { get; set; }

        public Vector3? Offset { get; set; }

        public float? Opacity { get; set; }

        public CompositionDropShadowSourcePolicy? SourcePolicy { get; set; }

        public override CompositionObjectType Type => CompositionObjectType.DropShadow;
    }
}
