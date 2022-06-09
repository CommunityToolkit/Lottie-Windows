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

        static float CrossProduct(SnVector2 v1, SnVector2 v2)
        {
            return (v1.X * v2.Y) - (v1.Y * v2.X);
        }

        // Checks if two vectors are colinear.
        static bool AlmostColinear(SnVector2 v1, SnVector2 v2)
        {
            // Epsilon is chosen so that vectors that are very close to being
            // colinear are also considered as colinear, since sometimes After Effects
            // outputs bezier curves with small precision, which may result in slight deviation.
            return Math.Abs(CrossProduct(v1, v2) / (1 + v1.Length() + v2.Length())) < 1e-2;
        }

        /// <summary>
        /// Gets a value indicating whether all of the control points are on the same line.
        /// </summary>
        bool IsColinear
        {
            get
            {
                // In order for 4 points to be colinear, any two pairs of directions
                // between them should be colinear.
                return AlmostColinear(_p1 - _p0, _p3 - _p0) && AlmostColinear(_p2 - _p3, _p0 - _p3);
            }
        }

        /// <inheritdoc/>
        // (1-t)^3P0 + 3(1-t)^2tP1 + 3(1-t)t^2P2 + t^3P3
        //
        // TODO: This also needs some kind of "arc-length parametrization" (https://pomax.github.io/bezierinfo/#tracing)
        // because currently point does not move along the curve with a uniform speed.
        // This is probably impossible to achieve because translation approach limits us to Windown Composition API
        // which does not support this feature, and probably won't, because it is very expensive to compute.
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
