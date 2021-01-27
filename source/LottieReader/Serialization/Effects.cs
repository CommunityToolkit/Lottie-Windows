// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Effect[] ReadEffectsList(in LottieJsonArrayElement? array, string layerName)
        {
            ArrayBuilder<Effect> result = default;
            if (array != null)
            {
                var effectsJsonCount = array.Value.Count;
                if (effectsJsonCount > 0)
                {
                    result.SetCapacity(effectsJsonCount);

                    for (var i = 0; i < effectsJsonCount; i++)
                    {
                        var effectObject = array.Value[i].AsObject();
                        if (effectObject != null)
                        {
                            result.AddItemIfNotNull(ReadEffect(effectObject.Value, layerName));
                        }
                    }
                }
            }

            return result.ToArray();
        }

        Effect? ReadEffect(in LottieJsonObjectElement obj, string layerName)
        {
            var effectType = obj.DoublePropertyOrNull("ty") ?? throw ReaderException("Invalid effect.");
            var effectName = obj.StringPropertyOrNull("nm") ?? string.Empty;
            var isEnabled = obj.BoolPropertyOrNull("en") ?? true;

            switch (effectType)
            {
                case 25:
                    return ReadDropShadowEffect(obj, effectName, isEnabled);

                case 29:
                    return ReadGaussianBlurEffect(obj, effectName, isEnabled);

                default:
                    obj.IgnorePropertyThatIsNotYetSupported(
                        "mn",   // match name.
                        "np",   // unknown.
                        "ix",   // index.
                        "ef");  // effect parameters.

                    _issues.LayerEffectsIsNotSupported(layerName, effectType.ToString());
                    return new Effect.Unknown(effectType, effectName, isEnabled);
            }
        }

        // Layer effect type 29.
        GaussianBlurEffect ReadGaussianBlurEffect(in LottieJsonObjectElement obj, string effectName, bool isEnabled)
        {
            // Index.
            obj.IgnorePropertyIntentionally("ix");

            // Match name.
            obj.IgnorePropertyIntentionally("mn");

            // Unknown.
            obj.IgnorePropertyIntentionally("np");

            var parameters = obj.ArrayPropertyOrNull("ef") ?? throw ParseFailure();

            Animatable<double>? blurriness = null;
            Animatable<Enum<BlurDimension>>? blurDimensions = null;
            Animatable<bool>? repeatEdgePixels = null;
            bool? forceGpuRendering = null;

            for (var i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i].AsObject() ?? throw ParseFailure();

                var value = p.ObjectPropertyOrNull("v") ?? throw ParseFailure();

                switch (i)
                {
                    case 0:
                        // Blurriness - float 0..50
                        blurriness = ReadAnimatableFloat(value) ?? throw ParseFailure();
                        break;

                    case 1:
                        blurDimensions = ReadAnimatableBlurDimension(value) ?? throw ParseFailure();
                        break;

                    case 2:
                        repeatEdgePixels = ReadAnimatableBool(value) ?? throw ParseFailure();
                        break;

                    case 3:
                        // Optional parameter. Parse it as an animatable because it has the
                        // animatable format, however it would be very strange if the value
                        // was actually animated so convert it to non-animated bool.
                        forceGpuRendering = ReadAnimatableBool(value)?.InitialValue ?? throw ParseFailure();
                        break;

                    default:
                        throw ParseFailure();
                }
            }

            // Ensure all required parameter values were provided.
            if (blurriness is null ||
                blurDimensions is null ||
                repeatEdgePixels is null)
            {
                throw ParseFailure();
            }

            return new GaussianBlurEffect(
                effectName,
                isEnabled,
                blurriness: blurriness,
                blurDimensions: blurDimensions,
                repeatEdgePixels: repeatEdgePixels,
                forceGpuRendering: forceGpuRendering);

            static Exception ParseFailure() => ReaderException("Invalid Gaussian blur effect.");
        }

        // Layer effect type 25.
        DropShadowEffect ReadDropShadowEffect(in LottieJsonObjectElement obj, string effectName, bool isEnabled)
        {
            // Index.
            obj.IgnorePropertyIntentionally("ix");

            // Match name.
            obj.IgnorePropertyIntentionally("mn");

            // Unknown.
            obj.IgnorePropertyIntentionally("np");

            var parameters = obj.ArrayPropertyOrNull("ef") ?? throw ParseFailure();

            Animatable<Color>? color = null;
            Animatable<Opacity>? opacity = null;
            Animatable<Rotation>? direction = null;
            Animatable<double>? distance = null;
            Animatable<double>? softness = null;
            Animatable<bool>? isShadowOnly = null;

            for (var i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i].AsObject() ?? throw ParseFailure();

                var value = p.ObjectPropertyOrNull("v") ?? throw ParseFailure();

                switch (i)
                {
                    case 0:
                        color = ReadAnimatableColor(value) ?? throw ParseFailure();
                        break;
                    case 1:
                        opacity = ReadAnimatableOpacityByte(value) ?? throw ParseFailure();
                        break;
                    case 2:
                        direction = ReadAnimatableRotation(value) ?? throw ParseFailure();
                        break;
                    case 3:
                        distance = ReadAnimatableFloat(value) ?? throw ParseFailure();
                        break;
                    case 4:
                        softness = ReadAnimatableFloat(value) ?? throw ParseFailure();
                        break;
                    case 5:
                        isShadowOnly = ReadAnimatableBool(value) ?? throw ParseFailure();
                        break;

                    default:
                        throw ParseFailure();
                }
            }

            // Ensure all parameter values were provided.
            if (direction is null ||
                color is null ||
                distance is null ||
                isShadowOnly is null ||
                opacity is null ||
                softness is null)
            {
                throw ParseFailure();
            }

            return new DropShadowEffect(
                effectName,
                isEnabled,
                color: color,
                direction: direction,
                distance: distance,
                isShadowOnly: isShadowOnly,
                opacity: opacity,
                softness: softness);

            static Exception ParseFailure() => ReaderException("Invalid drop shadow effect.");
        }
    }
}