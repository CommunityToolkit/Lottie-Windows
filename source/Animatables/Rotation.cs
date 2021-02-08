// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// A rotation value.
    /// </summary>
#if PUBLIC_Animatables
    public
#endif
    readonly struct Rotation : IEquatable<Rotation>
    {
        Rotation(double degrees)
        {
            Degrees = degrees;
        }

        public double Degrees { get; }

        public double Radians => Math.PI * Degrees / 180.0;

        public static Rotation None => new Rotation(0);

        public static Rotation FromDegrees(double value) => new Rotation(value);

        public static Rotation FromRadians(double value) => new Rotation(value * 180 / Math.PI);

        /// <summary>
        /// Returns the result of rotating <paramref name="point"/> around <paramref name="axis"/>.
        /// </summary>
        /// <returns>The resulting point.</returns>
        public Vector2 RotatePoint(Vector2 point, Vector2 axis)
        {
            var cosTheta = Math.Cos(-Radians);
            var sinTheta = Math.Sin(-Radians);

            // Normalize the point so that it is rotated around 0,0.
            var normalizedPoint = point - axis;

            // Calculate the new X and Y values.
            var newX = (cosTheta * normalizedPoint.X) - (sinTheta * normalizedPoint.Y);
            var newY = (sinTheta * normalizedPoint.X) + (cosTheta * normalizedPoint.Y);

            // Compensate for the normalization.
            var result = axis + new Vector2(newX, newY);

            return result;
        }

        public bool Equals(Rotation other) => other.Degrees == Degrees;

        public override bool Equals(object? obj) => obj is Rotation other && Equals(other);

        public override int GetHashCode() => Degrees.GetHashCode();

        public override string ToString() => $"{Degrees}°";

        public static Rotation operator +(Rotation left, Rotation right) => Rotation.FromDegrees(left.Degrees + right.Degrees);

        public static Rotation operator -(Rotation left, Rotation right) => Rotation.FromDegrees(left.Degrees - right.Degrees);

        public static bool operator ==(Rotation left, Rotation right) => left.Degrees == right.Degrees;

        public static bool operator !=(Rotation left, Rotation right) => left.Degrees != right.Degrees;
    }
}
