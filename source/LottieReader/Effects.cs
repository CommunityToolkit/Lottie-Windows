// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.CompilerServices;

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

            return result.ToArray();
        }

        // See: https://github.com/airbnb/lottie-web/blob/master/docs/json/effects/layer.json.
        Effect? ReadEffect(in LottieJsonObjectElement obj, string layerName)
        {
            var effectType = obj.DoublePropertyOrNull("ty");

            switch (effectType)
            {
                // DropShadows are type 25. This is the only type we currently support.
                case 25:
                    return ReadDropShadoEffect(obj);

                default:
                    obj.IgnorePropertyThatIsNotYetSupported(
                        "nm",   // name.
                        "mn",   // match name.
                        "en",   // enabled.
                        "n",
                        "ix");  // index.
                    obj.IgnorePropertyThatIsNotYetSupported(
                        "ef"); // effect parameters

                    _issues.LayerEffectsIsNotSupported(layerName);
                    return null;
            }
        }

        DropShadowEffect ReadDropShadoEffect(in LottieJsonObjectElement obj)
        {
            obj.IgnorePropertyIntentionally("ix");
            obj.IgnorePropertyIntentionally("mn");
            obj.IgnorePropertyIntentionally("n");

            var effectName = obj.StringPropertyOrNull("nm") ?? string.Empty;

            var isEnabled = obj.BoolPropertyOrNull("en") ?? true;

            // TODO - what if there are no parameters? Need to throw or somethign.
            var parameters = obj.ArrayPropertyOrNull("ef");

            Animatable<Color>? color = null;
            Animatable<Opacity>? opacity = null;
            Animatable<Rotation>? direction = null;
            Animatable<double>? distance = null;
            Animatable<double>? softness = null;

            for (var i = 0; i < parameters!.Value.Count; i++)
            {
                // TODO - what if this is null?
                var p = parameters!.Value[i].AsObject();
                var value = p!.Value.ObjectPropertyOrNull("v");

                switch (i)
                {
                    case 0:
                        color = ReadAnimatableColor(value);
                        break;

                    // Opacity.
                    case 1:
                        opacity = ReadAnimatableOpacity(value);
                        break;

                    // Direction
                    case 2:
                        direction = ReadAnimatableRotation(value);
                        break;

                    // Distance
                    case 3:
                        distance = ReadAnimatableFloat(value);
                        break;

                    // Softness
                    case 4:
                        softness = ReadAnimatableFloat(value);
                        break;

                    // IsShadowOnly
                    case 5:

                        // TODO
                        break;

                    // TODO - throw
                    default:
                        break;
                }
            }

            // TODO - deal with any of these not having values. Throw!
            return new DropShadowEffect(
                effectName,
                isEnabled,
                direction: direction!,
                color: color!,
                distance: distance!,
                isShadowOnly: false,
                opacity: opacity!,
                softness: softness!);
        }
    }
}