// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using SnVector2 = System.Numerics.Vector2;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    /// <summary>
    /// A cubic Bezier function with type Vector2.
    /// </summary>
    sealed class CubicBezierFunction2 : Vector2
    {
        readonly SnVector2 _p0;
        readonly SnVector2 _p1;
        readonly SnVector2 _p2;
        readonly SnVector2 _p3;
        readonly Scalar _t;

        public static CubicBezierFunction2 Create(SnVector2 controlPoint0, SnVector2 controlPoint1, SnVector2 controlPoint2, SnVector2 controlPoint3, Scalar t)
        {
            return new CubicBezierFunction2(controlPoint0, controlPoint1, controlPoint2, controlPoint3, t);
        }

        CubicBezierFunction2(SnVector2 controlPoint0, SnVector2 controlPoint1, SnVector2 controlPoint2, SnVector2 controlPoint3, Scalar t)
        {
            _p0 = controlPoint0;
            _p1 = controlPoint1;
            _p2 = controlPoint2;
            _p3 = controlPoint3;
            _t = t;
        }

        /// <summary>
        /// Gets a <see cref="CubicBezierFunction2"/> that describes a line from 0 to 0.
        /// </summary>
        public static CubicBezierFunction2 ZeroBezier { get; } = Create(SnVector2.Zero, SnVector2.Zero, SnVector2.Zero, SnVector2.Zero, 0);

        public override int OperationsCount => _t.OperationsCount;

        /// <summary>
        /// Gets a value indicating whether the cubic Bezier is equivalent to a line drawn from point 0 to point 3.
        /// </summary>
        public bool IsEquivalentToLinear
        {
            get
            {
                if (!IsColinear)
                {
                    return false;
                }

                // The points are on the same line. The cubic Bezier is a line if
                // p1 and p2 are between p0..p3.
                if (!IsBetween(_p0.X, _p1.X, _p2.X, _p3.X))
                {
                    return false;
                }

                if (!IsBetween(_p0.Y, _p1.Y, _p2.Y, _p3.Y))
                {
                    return false;
                }

                return true;
            }
        }

        // Returns true iff b and c and between a and d.
        static bool IsBetween(double a, double b, double c, double d)
        {
            var deltaAD = Math.Abs(a - d);

            if (Math.Abs(a - b) > deltaAD)
            {
                return false;
            }

            if (Math.Abs(d - b) > deltaAD)
            {
                return false;
            }

            if (Math.Abs(a - c) > deltaAD)
            {
                return false;
            }

            if (Math.Abs(d - c) > deltaAD)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether all of the control points are on the same line.
        /// </summary>
        bool IsColinear
        {
            get
            {
                var p01X = _p0.X - _p1.X;
                var p01Y = _p0.Y - _p1.Y;

                var p02X = _p0.X - _p2.X;
                var p02Y = _p0.Y - _p2.Y;

                var p03X = _p0.X - _p3.X;
                var p03Y = _p0.Y - _p3.Y;

                if (p01Y == 0 || p02Y == 0 || p03Y == 0)
                {
                    // Can't divide by Y because it's 0 in at least one case. (i.e. horizontal line)
                    if (p01X == 0 || p02X == 0 || p03X == 0)
                    {
                        // Can't divide by X because it's 0 in at least one case (i.e. vertical line)
                        // The points can only be colinear if they're all equal.
                        return p01X == p02X && p02X == p03X && p03X == p01X;
                    }
                    else
                    {
                        return (p01Y / p01X) == (p02Y / p02X) &&
                               (p01Y / p01X) == (p03Y / p03X);
                    }
                }
                else
                {
                    return (p01X / p01Y) == (p02X / p02Y) &&
                           (p01X / p01Y) == (p03X / p03Y);
                }
            }
        }

        /// <inheritdoc/>
        // (1-t)^3P0 + 3(1-t)^2tP1 + 3(1-t)t^2P2 + t^3P3
        protected override Vector2 Simplify()
        {
            var oneMinusT = 1 - _t;

            // (1-t)^3P0
            var p0Part = Pow(oneMinusT, 3) * Vector2(_p0);

            // (1-t)^2t3P1
            var p1Part = 3 * Squared(oneMinusT) * _t * Vector2(_p1);

            // (1-t)t^23P2
            var p2Part = 3 * oneMinusT * Squared(_t) * Vector2(_p2);

            // t^3P3
            var p3Part = Pow(_t, 3) * Vector2(_p3);

            return (p0Part + p1Part + p2Part + p3Part).Simplified;
        }

        /// <inheritdoc/>
        protected override string CreateExpressionText() => ToText();

        /// <summary>
        /// Returns the <see cref="CubicBezierFunction2"/> as a <see cref="Vector3"/> expression where
        /// the Z values are 0.
        /// </summary>
        /// <returns>The <see cref="CubicBezierFunction2"/> as a <see cref="Vector3"/> expression where
        /// the Z values are 0.</returns>
        internal Vector3 AsVector3() => new CubicBezierFunctionAsVector3(this);

        // A Vector3-typed CubicBezierFunction2. The Z values are always 0.
        sealed class CubicBezierFunctionAsVector3 : Vector3
        {
            readonly CubicBezierFunction2 _original;

            internal CubicBezierFunctionAsVector3(CubicBezierFunction2 original)
            {
                _original = original;
            }

            /// <inheritdoc/>
            public override int OperationsCount => _original.OperationsCount;

            /// <inheritdoc/>
            // (1-t)^3P0 + 3(1-t)^2tP1 + 3(1-t)t^2P2 + t^3P3
            protected override Vector3 Simplify()
            {
                var oneMinusT = 1 - _original._t;

                // (1-t)^3P0
                var p0Part = Pow(oneMinusT, 3) * Vector3(_original._p0);

                // 3(1-t)^2tP1
                var p1Part = 3 * Squared(oneMinusT) * _original._t * Vector3(_original._p1);

                // 3(1-t)t^2P2
                var p2Part = 3 * oneMinusT * Squared(_original._t) * Vector3(_original._p2);

                // t^3P3
                var p3Part = Pow(_original._t, 3) * Vector3(_original._p3);

                return (p0Part + p1Part + p2Part + p3Part).Simplified;
            }

            /// <inheritdoc/>
            protected override string CreateExpressionText() => ToText();
        }
    }
}
