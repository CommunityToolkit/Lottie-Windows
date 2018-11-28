// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class Vector2KeyFrameAnimation : KeyFrameAnimation<Vector2>
    {
        internal Vector2KeyFrameAnimation()
            : base(null)
        {
        }

        Vector2KeyFrameAnimation(Vector2KeyFrameAnimation other)
            : base(other)
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.Vector2KeyFrameAnimation;

        internal override CompositionAnimation Clone() => new Vector2KeyFrameAnimation(this);
    }
}
