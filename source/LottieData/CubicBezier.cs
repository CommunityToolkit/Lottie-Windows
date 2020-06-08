// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Describes a cubic Bezier function as the 2nd and 3rd control points where
    /// the 1st and 4th control points are 0,0 and 1,1 respectively.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    readonly struct CubicBezier : IEquatable<CubicBezier>
    {
        public CubicBezier(Vector2 controlPoint1, Vector2 controlPoint2)
        {
            ControlPoint1 = controlPoint1;
            ControlPoint2 = controlPoint2;
        }

        /// <summary>
        /// The 2nd control point.
        /// </summary>
        public Vector2 ControlPoint1 { get; }

        /// <summary>
        /// The 3rd control point.
        /// </summary>
        public Vector2 ControlPoint2 { get; }

        /// <summary>
        /// True if the <see cref="CubicBezier"/> represents a linear function from
        /// 0,0 to 1,1. A linear function requires that <see cref="ControlPoint1"/>
        /// and <see cref="ControlPoint2"/> line on the line that passes through 0,0, and 1,1.
        /// </summary>
        public bool IsLinear => ControlPoint1.X == ControlPoint1.Y && ControlPoint2.X == ControlPoint2.Y;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CubicBezier other && other == this;

        /// <inheritdoc/>
        public bool Equals(CubicBezier other) =>
                ControlPoint1.Equals(other.ControlPoint1) &&
                ControlPoint2.Equals(other.ControlPoint2);

        /// <inheritdoc/>
        public override int GetHashCode() => ControlPoint1.GetHashCode() ^ ControlPoint2.GetHashCode();

        public static bool operator ==(CubicBezier a, CubicBezier b) =>
            a.ControlPoint1 == b.ControlPoint1 && a.ControlPoint2 == b.ControlPoint2;

        public static bool operator !=(CubicBezier a, CubicBezier b) => !(a == b);

        /// <inheritdoc/>
        public override string ToString() => $"{ControlPoint1},{ControlPoint2}";
    }
}
