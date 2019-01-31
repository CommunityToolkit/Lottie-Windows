// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    enum CanvasComposite
    {
        SourceOver = 0,
        DestinationOver = 1,
        SourceIn = 2,
        DestinationIn = 3,
        SourceOut = 4,
        DestinationOut = 5,
        SourceAtop = 6,
        DestinationAtop = 7,
        Xor = 8,
        Add = 9,
        Copy = 10,
        BoundedCopy = 11,
        MaskInvert = 12,
    }
}
