// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Provides access to the effects for a <see cref="Layer"/>.
    /// Reports issues for effects that are not supported, and ignores
    /// effects that are disabled.
    /// </summary>
    sealed class Effects
    {
        readonly LayerContext _context;

        internal Effects(LayerContext context)
        {
            _context = context;

            // Validate the effects, and save the valid ones for use by the layer translator.
            foreach (var effect in context.Layer.Effects.Where(e => e.IsEnabled))
            {
                switch (effect.Type)
                {
                    case Effect.EffectType.DropShadow:
                        if (DropShadowEffect != null)
                        {
                            // Emit an issue about there being more than one.
                            context.Issues.RepeatedLayerEffect("Drop shadow");
                        }

                        DropShadowEffect = (DropShadowEffect)effect;
                        break;
                    case Effect.EffectType.GaussianBlur:
                        var gaussianBlurEffect = (GaussianBlurEffect)effect;

                        // Ignore if the effect has no blurriness. It is effectively disabled.
                        var trimmedBlurriness = Optimizer.TrimAnimatable(context, gaussianBlurEffect.Blurriness);
                        if (!trimmedBlurriness.IsAlways(0))
                        {
                            if (GaussianBlurEffect != null)
                            {
                                // Emit an issue about there being more than one.
                                context.Issues.RepeatedLayerEffect("Gaussian blur");
                            }

                            GaussianBlurEffect = gaussianBlurEffect;
                        }

                        break;
                    default:
                        EmitIssueAboutUnsupportedEffect(effect.Type.ToString());
                        break;
                }
            }
        }

        public DropShadowEffect? DropShadowEffect { get; }

        public GaussianBlurEffect? GaussianBlurEffect { get; }

        /// <summary>
        /// If there is a drop shadow effect, emit an issue about it not being
        /// supported.
        /// </summary>
        internal void EmitIssueIfDropShadow() =>
            EmitIssueAboutUnsupportedEffect(DropShadowEffect, "drop shadow");

        /// <summary>
        /// If there is a Gaussian blur effect, emit an issue about it not being
        /// supported.
        /// </summary>
        internal void EmitIssueIfGaussianBlur() =>
            EmitIssueAboutUnsupportedEffect(GaussianBlurEffect, "Gaussian blur");

        // If the given effect is not null, emit an issue about the effect not
        // being supported on this layer.
        void EmitIssueAboutUnsupportedEffect(Effect? effect, string effectName)
        {
            if (effect != null)
            {
                EmitIssueAboutUnsupportedEffect(effectName);
            }
        }

        // Emit an issue about the effect not being supported on this layer.
        void EmitIssueAboutUnsupportedEffect(string effectName) =>
                _context.Issues.LayerEffectNotSupportedOnLayer(effectName, _context.Layer.Type.ToString());
    }
}
