// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// An animatable Vector3 value expressed as 3 animatable floating point values.
    /// </summary>
#if PUBLIC_IR
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

        public AnimatableXYZ WithOffset(Vector3 offset)
            => new AnimatableXYZ(X.Select(x => x + offset.X), Y.Select(y => y + offset.Y), Z.Select(z => z + offset.Z));

        public AnimatableXYZ WithTimeOffset(double timeOffset)
            => timeOffset == 0
                ? this
                : new AnimatableXYZ(X.WithTimeOffset(timeOffset), Y.WithTimeOffset(timeOffset), Z.WithTimeOffset(timeOffset));

        IAnimatableVector3 IAnimatableVector3.WithTimeOffset(double timeOffset)
            => WithTimeOffset(timeOffset);

        IAnimatableValue<Vector3> IAnimatableValue<Vector3>.WithTimeOffset(double timeOffset)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public bool IsAnimated => X.IsAnimated || Y.IsAnimated || Z.IsAnimated;
    }
}
