// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using SnVector2 = System.Numerics.Vector2;

namespace CommunityToolkit.WinUI.Lottie.Animatables
{
#if PUBLIC_Animatables
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

        public static Vector2 operator /(Vector2 left, double right) =>
            new Vector2(left.X / right, left.Y / right);

        public static Vector2 operator +(Vector2 left, Vector2 right) =>
            new Vector2(left.X + right.X, left.Y + right.Y);

        public static Vector2 operator -(Vector2 left, Vector2 right) =>
            new Vector2(left.X - right.X, left.Y - right.Y);

        public static Vector2 operator -(Vector2 value) =>
            new Vector2(-value.X, -value.Y);

        public static Vector2 operator *(Vector2 left, Vector2 right) =>
            new Vector2(left.X * right.X, left.Y * right.Y);

        public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);

        public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);

        /// <summary>
        /// Implicit conversion from <see cref="System.Numerics.Vector2"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        public static implicit operator Vector2(SnVector2 value) => new Vector2(value.X, value.Y);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);

        /// <inheritdoc/>
        public bool Equals(Vector2 other) => X == other.X && Y == other.Y;

        /// <inheritdoc/>
        public override int GetHashCode() => (X * Y).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"{{{X},{Y}}}";

        public double Length() => Math.Sqrt((X * X) + (Y * Y));

        public Vector2 Normalized() => this / Length();
    }
}
