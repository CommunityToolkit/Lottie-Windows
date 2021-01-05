// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// A rotation value.
    /// </summary>
#if PUBLIC_IR
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

        public bool Equals(Rotation other) => other.Degrees == Degrees;

        public override bool Equals(object? obj) => obj is Rotation other && Equals(other);

        public override int GetHashCode() => Degrees.GetHashCode();

        public override string ToString() => $"{Degrees}°";
    }
}
