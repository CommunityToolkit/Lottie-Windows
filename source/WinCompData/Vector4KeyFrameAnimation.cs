// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class Vector4KeyFrameAnimation : KeyFrameAnimation<Vector4>
    {
        internal Vector4KeyFrameAnimation()
            : base(null)
        {
        }

        Vector4KeyFrameAnimation(Vector4KeyFrameAnimation other)
            : base(other)
        {
        }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.Vector4KeyFrameAnimation;

        internal override CompositionAnimation Clone() => new Vector4KeyFrameAnimation(this);
    }
}
