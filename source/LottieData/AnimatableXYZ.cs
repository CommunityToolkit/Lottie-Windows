// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// An animatable Vector3 value expressed as 3 animatable floating point values.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class AnimatableXYZ : IAnimatableVector3
    {
        public AnimatableXYZ(Animatable<double> x, Animatable<double> y, Animatable<double> z)
        {
            InitialValue = new Vector3(x.InitialValue, y.InitialValue, z.InitialValue);
            X = x;
            Y = y;
            Z = z;
        }

        /// <inheritdoc/>
        public AnimatableVector3Type Type => AnimatableVector3Type.XYZ;

        /// <inheritdoc/>
        public Vector3 InitialValue { get; }

        public Animatable<double> X { get; }

        public Animatable<double> Y { get; }

        public Animatable<double> Z { get; }

        /// <inheritdoc/>
        public bool IsAnimated => X.IsAnimated || Y.IsAnimated || Z.IsAnimated;
    }
}
