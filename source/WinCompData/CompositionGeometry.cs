// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    abstract class CompositionGeometry : CompositionObject
    {
        protected private CompositionGeometry()
        {
        }

        // Default = 1
        public float TrimEnd { get; set; } = 1;

        // Default = 0
        public float TrimOffset { get; set; }

        // Default = 0
        public float TrimStart { get; set; }
    }
}
