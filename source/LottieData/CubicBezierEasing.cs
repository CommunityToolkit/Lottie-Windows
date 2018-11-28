// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class CubicBezierEasing : Easing, IEquatable<CubicBezierEasing>
    {
        public CubicBezierEasing(Vector3 controlPoint1, Vector3 controlPoint2)
        {
            ControlPoint1 = controlPoint1;
            ControlPoint2 = controlPoint2;
        }

        public Vector3 ControlPoint1 { get; }

        public Vector3 ControlPoint2 { get; }

        /// <inheritdoc/>
        public override EasingType Type => EasingType.CubicBezier;

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CubicBezierEasing);

        /// <inheritdoc/>
        public bool Equals(CubicBezierEasing other) =>
               ReferenceEquals(this, other) ||
                (other != null &&
                ControlPoint1.Equals(other.ControlPoint1) &&
                ControlPoint2.Equals(other.ControlPoint2));

        /// <inheritdoc/>
        public override int GetHashCode() => ControlPoint1.GetHashCode() ^ ControlPoint2.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => nameof(CubicBezierEasing);
    }
}
