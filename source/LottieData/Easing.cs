// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class Easing : IEquatable<Easing>
    {
        private protected Easing()
        {
        }

        public abstract EasingType Type { get; }

        /// <inheritdoc/>
        public bool Equals(Easing? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is null)
            {
                return false;
            }

            if (Type != other.Type)
            {
                return false;
            }

            switch (Type)
            {
                case EasingType.Hold:
                case EasingType.Linear:
                    // Linear and hold easings have no parameters, so they're all equivalent to each other.
                    return true;
                case EasingType.CubicBezier:
                    var xCb = (CubicBezierEasing)this;
                    var yCb = (CubicBezierEasing)other;
                    return xCb.Equals(yCb);
                default:
                    throw new InvalidOperationException();
            }
        }

        public static bool operator ==(Easing? a, Easing? b) => (a is Easing && a.Equals(b)) || (a is null && b is null);

        public static bool operator !=(Easing? a, Easing? b) => !(a == b);

        /// <inheritdoc/>
        public override abstract bool Equals(object? obj);

        /// <inheritdoc/>
        public override abstract int GetHashCode();

        public enum EasingType
        {
            Linear,
            CubicBezier,
            Hold,
        }
    }
}
