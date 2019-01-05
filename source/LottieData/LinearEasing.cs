// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class LinearEasing : Easing, IEquatable<LinearEasing>
    {
        LinearEasing()
        {
        }

        public static LinearEasing Instance { get; } = new LinearEasing();

        /// <inheritdoc/>
        public override EasingType Type => EasingType.Linear;

        /// <inheritdoc/>
        public override string ToString() => nameof(LinearEasing);

        /// <inheritdoc/>
        // All LinearEasings are equivalent.
        public override int GetHashCode() => (int)Type;

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as LinearEasing);

        /// <inheritdoc/>
        // All LinearEasings are equivalent.
        public bool Equals(LinearEasing other) => other != null;
    }
}
