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
#if !WINDOWS_UWP
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

        // All SetpEeasings are equivalent.
        /// <inheritdoc/>
        public override int GetHashCode() => (int)Type;

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as HoldEasing);

        // All LinearEasings are equivalent.
        /// <inheritdoc/>
        public bool Equals(HoldEasing other) => other != null;

        /// <inheritdoc/>
        public override string ToString() => nameof(HoldEasing);
    }
}
