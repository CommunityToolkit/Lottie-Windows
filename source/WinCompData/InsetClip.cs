// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class InsetClip : CompositionClip
    {
        internal InsetClip()
        {
        }

        // Default is 0.
        public float LeftInset { get; set; }

        // Default is 0.
        public float RightInset { get; set; }

        // Default is 0.
        public float BottomInset { get; set; }

        // Default is 0.
        public float TopInset { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.InsetClip;
    }
}
