// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if PUBLIC_WinCompData
    public
#endif
    abstract class KeyFrameAnimation_ : CompositionAnimation
    {
        public TimeSpan Duration { get; set; }

        public abstract int KeyFrameCount { get; }

        private protected KeyFrameAnimation_(KeyFrameAnimation_ other)
            : base(other)
        {
        }
    }
}