// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    // Methods for decomposing Matrix3x2 and Matrix4x4 into offset,
    // rotation, scale, etc. Used for creating helpful comments that
    // describe the purpose of a matrix.
    static class MatrixDecomposer
    {
        internal static (Vector2? translation, double? rotationDegrees, Vector2? scale, Vector2? skew) Decompose(in Matrix3x2 matrix)
        {
            Vector2? translation;
            double? rotationDegrees;
            Vector2? scale;
            Vector2? skew;

            var a = matrix.M11;
            var b = matrix.M12;
            var c = matrix.M21;
            var d = matrix.M22;

            translation = Vector2OrNullIfZero(matrix.M31, matrix.M32);

            var delta = (a * d) - (b * c);

            if (a != 0 || b != 0)
            {
                var r = Math.Sqrt((a * a) + (b * b));
                rotationDegrees = RadiansToDegrees(b > 0 ? Math.Acos(a / r) : -Math.Acos(a / r));
                scale = Vector2OrNullIfOne(r, delta / r);
                skew = Vector2OrNullIfZero(Math.Atan(((a * c) + (b * d)) / (r * r)), 0);
            }
            else if (c != 0 || d != 0)
            {
                var s = Math.Sqrt((c * c) + (d * d));
                rotationDegrees = RadiansToDegrees((Math.PI / 2) - (d > 0 ? Math.Acos(-c / s) : -Math.Acos(c / s)));
                scale = Vector2OrNullIfOne(delta / s, s);
                skew = Vector2OrNullIfZero(0, Math.Atan(((a * c) + (b * d)) / (s * s)));
            }
            else
            {
                rotationDegrees = null;
                scale = null;
                skew = null;
            }

            if (rotationDegrees.HasValue)
            {
                if (double.IsNaN(rotationDegrees.Value))
                {
                    // The rotation value had an error. Ignore it.
                    rotationDegrees = null;
                }
                else if (rotationDegrees.Value == 0)
                {
                    // 0 rotation is not interesting. Ignore it.
                    rotationDegrees = null;
                }
            }

            return (translation, rotationDegrees, scale, skew);
        }

        internal static (Vector3? translation, Quaternion? rotation, Vector3? scale) Decompose(in Matrix4x4 matrix)
        {
            var t = default(Vector3?);
            var r = default(Quaternion?);
            var s = default(Vector3?);

            if (Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
            {
                if (translation != Vector3.Zero)
                {
                    t = translation;
                }

                if (rotation != Quaternion.Identity)
                {
                    r = rotation;
                }

                if (scale != Vector3.One)
                {
                    s = scale;
                }
            }

            return (t, r, s);
        }

        static Vector2? Vector2OrNullIfZero(double x, double y)
           => x == 0 && y == 0
                ? (Vector2?)null
                : new Vector2((float)x, (float)y);

        static Vector2? Vector2OrNullIfOne(double x, double y)
            => x == 1 && y == 1
                ? (Vector2?)null
                : new Vector2((float)x, (float)y);

        static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;
    }
}