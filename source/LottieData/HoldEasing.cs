// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// An easing that holds the current value until the key frame time, then
    /// jumps to the key frame value.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class HoldEasing : Easing, IEquatable<HoldEasing>
    {
        HoldEasing()
        {
        }

        public static HoldEasing Instance { get; } = new HoldEasing();

        /// <inheritdoc/>
        public override EasingType Type => EasingType.Hold;

        /// <inheritdoc/>
        // All HoldEasings are equivalent.
        public override int GetHashCode() => (int)Type;

        /// <inheritdoc/>
        // All HoldEasings are equivalent.
        public override bool Equals(object obj) => obj is HoldEasing;

        /// <inheritdoc/>
        // All HoldEasings are equivalent.
        public bool Equals(HoldEasing other) => other is HoldEasing;

        public static bool operator ==(HoldEasing a, HoldEasing b) => a is HoldEasing && b is HoldEasing;

        public static bool operator !=(HoldEasing a, HoldEasing b) => !(a == b);

        /// <inheritdoc/>
        public override string ToString() => nameof(HoldEasing);
    }
}
