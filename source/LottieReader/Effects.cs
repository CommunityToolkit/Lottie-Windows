// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

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

        // See: https://github.com/airbnb/lottie-web/blob/master/docs/json/effects/layer.json.
        Effect? ReadEffect(in LottieJsonObjectElement obj, string layerName)
        {
            var effectType = obj.DoublePropertyOrNull("ty") ?? throw ReaderException("Invalid effect");

            switch (effectType)
            {
                // DropShadows are type 25. This is the only type we currently support.
                case 25:
                    return ReadDropShadowEffect(obj);

                default:
                    obj.IgnorePropertyThatIsNotYetSupported(
                        "nm",   // name.
                        "mn",   // match name.
                        "en",   // enabled.
                        "np",   // unknown.
                        "ix");  // index.
                    obj.IgnorePropertyThatIsNotYetSupported(
                        "ef"); // effect parameters

                    _issues.LayerEffectsIsNotSupported(layerName, effectType.ToString());
                    return null;
            }
        }

        DropShadowEffect ReadDropShadowEffect(in LottieJsonObjectElement obj)
        {
            // Index.
            obj.IgnorePropertyIntentionally("ix");

            // Match name.
            obj.IgnorePropertyIntentionally("mn");

            // Unknown.
            obj.IgnorePropertyIntentionally("np");

            var effectName = obj.StringPropertyOrNull("nm") ?? string.Empty;

            var isEnabled = obj.BoolPropertyOrNull("en") ?? true;

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

                    // Opacity.
                    case 1:
                        opacity = ReadAnimatableOpacity(value) ?? throw ParseFailure();
                        break;

                    // Direction
                    case 2:
                        direction = ReadAnimatableRotation(value) ?? throw ParseFailure();
                        break;

                    // Distance
                    case 3:
                        distance = ReadAnimatableFloat(value) ?? throw ParseFailure();
                        break;

                    // Softness
                    case 4:
                        softness = ReadAnimatableFloat(value) ?? throw ParseFailure();
                        break;

                    // IsShadowOnly
                    case 5:
                        isShadowOnly = ReadAnimatableBool(value) ?? throw ParseFailure();
                        break;

                    default:
                        throw ReaderException("Invalid drop shadow effect");
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
                direction: direction,
                color: color,
                distance: distance,
                isShadowOnly: isShadowOnly,
                opacity: opacity,
                softness: softness);

            static Exception ParseFailure() => ReaderException("Invalid drop shadow effect");
        }
    }
}