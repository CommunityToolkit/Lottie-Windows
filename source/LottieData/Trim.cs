// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// A rotation value.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    readonly struct Trim : IEquatable<Trim>
    {
        Trim(double value)
        {
            Value = value;
        }

        public double Value { get; }

        public double Percent => Value * 100;

        public static Trim None => new Trim(0);

        public static Trim FromPercent(double percent) => new Trim(percent / 100);

        public bool Equals(Trim other) => other.Value == Value;

        public override bool Equals(object obj) => obj is Trim other && Equals(other);

        public static bool operator ==(Trim a, Trim b) => a.Equals(b);

        public static bool operator !=(Trim a, Trim b) => !(a == b);

        public static bool operator <(Trim a, Trim b) => a.Value < b.Value;

        public static bool operator >(Trim a, Trim b) => a.Value > b.Value;

        public static bool operator <=(Trim a, Trim b) => a.Value <= b.Value;

        public static bool operator >=(Trim a, Trim b) => a.Value >= b.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"{Percent}%";
    }
}
