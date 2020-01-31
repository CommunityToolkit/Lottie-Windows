// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    readonly struct Vector2 : IEquatable<Vector2>
    {
        public Vector2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public readonly double X;
        public readonly double Y;

        public static readonly Vector2 Zero = new Vector2(0, 0);

        public static readonly Vector2 One = new Vector2(1, 1);

        public static Vector2 operator *(Vector2 left, double right) =>
            new Vector2(left.X * right, left.Y * right);

        public static Vector2 operator +(Vector2 left, Vector2 right) =>
            new Vector2(left.X + right.X, left.Y + right.Y);

        public static Vector2 operator -(Vector2 left, Vector2 right) =>
            new Vector2(left.X - right.X, left.Y - right.Y);

        public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);

        public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Vector2 other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(Vector2 other) => X == other.X && Y == other.Y;

        /// <inheritdoc/>
        public override int GetHashCode() => (X * Y).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"{{{X},{Y}}}";
    }
}