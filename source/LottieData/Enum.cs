// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Wraps an enum, but implemented as a struct so that
    /// it can implement <see cref="IEquatable{T}"/> as required by
    /// <see cref="Animatable{T}"/>.
    /// </summary>
    /// <typeparam name="T">An enum type.</typeparam>
#if PUBLIC_LottieData
    public
#endif
    readonly struct Enum<T> : IEquatable<Enum<T>>
        where T : struct, IComparable
    {
        readonly T _value;

        Enum(T value)
        {
            _value = value;
        }

        public T Value => _value;

        public static implicit operator Enum<T>(T value) => new Enum<T>(value);

        public static implicit operator T(Enum<T> value) => value;

        public bool Equals([AllowNull] Enum<T> other) => other._value.CompareTo(_value) == 0;

        public override string? ToString() => _value.ToString();
    }
}
