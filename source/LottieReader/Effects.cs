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
        Effect? ReadEffect(LottieJsonObjectElement obj, string layerName)
        {
            var name = obj.StringPropertyOrNull("nm") ?? string.Empty;

            obj.IgnorePropertyThatIsNotYetSupported(
                "mn",   // match name.
                "en",   // enabled.
                "n",
                "ix");  // index.

            var effectType = obj.DoublePropertyOrNull("ty");

            switch (effectType)
            {
                // DropShadows are type 25. This is the only type we currently support.
                case 25:
                    // TODO - save the parameters.
                    var effectParametersArray = ReadDropShadowEffectParameters(obj.ArrayPropertyOrNull("ef"));
                    return new DropShadowEffect(name);

                default:
                    _issues.LayerEffectsIsNotSupported(layerName);
                    obj.IgnorePropertyThatIsNotYetSupported("ef");
                    return null;
            }
        }

        static object[] ReadDropShadowEffectParameters(LottieJsonArrayElement? obj)
        {
            if (obj is null)
            {
                return Array.Empty<object>();
            }

            var result = new object[obj.Value.Count];
            for (var i = 0; i < obj.Value.Count; i++)
            {
                result[i] = obj.Value[i];
            }

            return result;
        }
    }
}