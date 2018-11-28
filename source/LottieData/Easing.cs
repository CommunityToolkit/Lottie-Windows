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
        protected private Easing()
        {
        }

        public abstract EasingType Type { get; }

        /// <inheritdoc/>
        public bool Equals(Easing other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
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
                    return xCb.ControlPoint1.Equals(yCb.ControlPoint1);
                default:
                    throw new InvalidOperationException();
            }
        }

        public enum EasingType
        {
            Linear,
            CubicBezier,
            Hold,
        }
    }
}
