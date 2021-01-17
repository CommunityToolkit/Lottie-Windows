// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// A drop shadow effect.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    sealed class DropShadowEffect : Effect
    {
        public DropShadowEffect(
            string name,
            bool isEnabled,
            Animatable<Color> color,
            Animatable<Rotation> direction,
            Animatable<double> distance,
            Animatable<bool> isShadowOnly,
            Animatable<Opacity> opacity,
            Animatable<double> softness)
            : base(
                  name,
                  isEnabled)
        {
            Direction = direction;
            Color = color;
            Distance = distance;
            IsShadowOnly = isShadowOnly;
            Opacity = opacity;
            Softness = softness;
        }

        /// <summary>
        /// The color of the shadow.
        /// </summary>
        public Animatable<Color> Color { get; }

        /// <summary>
        /// The angle from the shadow caster to the shadow.
        /// </summary>
        public Animatable<Rotation> Direction { get; }

        /// <summary>
        /// The distance from the shadow caster to the shadow.
        /// </summary>
        public Animatable<double> Distance { get; }

        /// <summary>
        /// If true, only the shadow will be viisble and the shadow caster
        /// will not be visible, otherwise both the shadow caster and the
        /// shadow will be visible.
        /// </summary>
        public Animatable<bool> IsShadowOnly { get; }

        /// <summary>
        /// The opacity of the shadow.
        /// </summary>
        public Animatable<Opacity> Opacity { get; }

        /// <summary>
        /// The softness of the shadow.
        /// </summary>
        public Animatable<double> Softness { get; }

        public override EffectType Type => EffectType.DropShadow;
    }
}
