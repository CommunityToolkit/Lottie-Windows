// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionGeometry : CompositionObject
    {
        private protected CompositionGeometry()
        {
        }

        // Default is 1.
        public float? TrimEnd { get; set; }

        // Default is 0.
        public float? TrimOffset { get; set; }

        // Default is 0.
        public float? TrimStart { get; set; }
    }
}
