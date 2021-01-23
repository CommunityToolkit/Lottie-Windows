// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
    /// <summary>
    /// An opacity value. 0 is transparent, 1 is opaque.
    /// </summary>
#if PUBLIC_Animatables
    public
#endif
    readonly struct Opacity : IEquatable<Opacity>
    {
        Opacity(double value)
        {
            Value = value;
        }

        public bool IsOpaque => Value == 1;

        public bool IsTransparent => Value == 0;

        public static Opacity Opaque { get; } = new Opacity(1);

        public double Percent => Value * 100;

        public static Opacity Transparent { get; } = new Opacity(0);

        public double Value { get; }

        public static Opacity FromByte(double value) => new Opacity(value / 255);

        public static Opacity FromFloat(double value) => new Opacity(value);

        public static Opacity FromPercent(double percent) => new Opacity(percent / 100);

        public static Opacity operator *(Opacity left, Opacity right) => new Opacity(left.Value * right.Value);

        public static Opacity operator *(Opacity opacity, double scale) => new Opacity(opacity.Value * scale);

        public static bool operator >(Opacity left, Opacity right) => left.Value > right.Value;

        public static bool operator <(Opacity left, Opacity right) => left.Value < right.Value;

        public static bool operator ==(Opacity left, Opacity right) => left.Value == right.Value;

        public static bool operator !=(Opacity left, Opacity right) => left.Value != right.Value;

        public bool Equals(Opacity other) => other.Value == Value;

        public override bool Equals(object? obj) => obj is Opacity other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"{Percent}%";
    }
}
