// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// An animatable Vector3 value expressed as a single animatable Vector3 value.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class AnimatableVector3 : Animatable<Vector3>, IAnimatableVector3
    {
        public AnimatableVector3(Vector3 initialValue, int? propertyIndex)
            : this(initialValue, EmptyKeyFrames, propertyIndex)
        {
        }

        public AnimatableVector3(Vector3 initialValue, IEnumerable<KeyFrame<Vector3>> keyframes, int? propertyIndex)
            : base(initialValue, keyframes, propertyIndex)
        {
        }

        /// <inheritdoc/>
        public AnimatableVector3Type Type => AnimatableVector3Type.Vector3;
    }
}