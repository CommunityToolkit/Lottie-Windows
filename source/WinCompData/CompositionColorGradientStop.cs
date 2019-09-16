// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(5)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionColorGradientStop : CompositionObject
    {
        internal CompositionColorGradientStop(float offset, Wui.Color color)
        {
            Color = color;
            Offset = offset;
        }

        public Wui.Color Color { get; set; }

        public float Offset { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionColorGradientStop;
    }
}
