// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class CubicBezierEasing : Easing, IEquatable<CubicBezierEasing>
    {
        public CubicBezierEasing(IEnumerable<CubicBezier> beziers)
        {
            Beziers = beziers.ToArray();
        }

        /// <summary>
        /// The cubic Beziers for each component of the animatable value.
        /// </summary>
        public IReadOnlyList<CubicBezier> Beziers { get; }

        /// <inheritdoc/>
        public override EasingType Type => EasingType.CubicBezier;

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CubicBezierEasing);

        /// <inheritdoc/>
        public bool Equals(CubicBezierEasing other) =>
               ReferenceEquals(this, other) ||
                (other is CubicBezierEasing &&
                Enumerable.SequenceEqual(Beziers, other.Beziers));

        /// <inheritdoc/>
        public override int GetHashCode() => Beziers.GetHashCode();

        public static bool operator ==(CubicBezierEasing a, CubicBezierEasing b) =>
            (a is CubicBezierEasing && a.Equals(b)) || (a is null && b is null);

        public static bool operator !=(CubicBezierEasing a, CubicBezierEasing b) =>
            !(a == b);

        /// <inheritdoc/>
        public override string ToString() => nameof(CubicBezierEasing);
    }
}
