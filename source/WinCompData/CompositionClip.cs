// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionClip : CompositionObject
    {
        private protected CompositionClip()
        {
        }

        // Default is 0, 0.
        public Vector2? CenterPoint { get; set; }

        // Default is 1, 1.
        public Vector2? Scale { get; set; }
    }
}
