// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        internal delegate T LottieJsonElementReader<T>(in LottieJsonElement element);

        internal readonly struct LottieJsonElement
        {
            readonly LottieCompositionReader _owner;
            readonly JsonElement _wrapped;

            internal LottieJsonElement(LottieCompositionReader owner, JsonElement wrapped)
            {
                _owner = owner;
                _wrapped = wrapped;
            }

            internal JsonValueKind Kind
                => _wrapped.ValueKind;

            internal LottieJsonArrayElement? AsArray()
                => _wrapped.ValueKind == JsonValueKind.Array
                    ? new LottieJsonArrayElement(_owner, _wrapped)
                    : (LottieJsonArrayElement?)null;

            internal LottieJsonObjectElement? AsObject()
                => _wrapped.ValueKind == JsonValueKind.Object
                    ? new LottieJsonObjectElement(_owner, _wrapped)
                    : (LottieJsonObjectElement?)null;

            internal bool? AsBoolean()
            {
                switch (_wrapped.ValueKind)
                {
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    default:
                        var number = AsDouble();
                        return number.HasValue
                            ? number != 0
                            : (bool?)null;
                }
            }

            internal double? AsDouble()
            {
                switch (_wrapped.ValueKind)
                {
                    case JsonValueKind.Number:
                        return _wrapped.GetDouble();
                    case JsonValueKind.String:
                        return double.TryParse(_wrapped.GetString(), out var result)
                            ? result
                            : (double?)null;

                    case JsonValueKind.Array:
                        {
                            var array = AsArray()!.Value;
                            switch (array.Count)
                            {
                                case 0:
                                    return null;
                                case 1:
                                    return array[0].AsDouble();
                                default:
                                    // Some Lottie files have multiple values in arrays that should only have one. Just
                                    // take the first value.
                                    return array[0].AsDouble();
                            }
                        }

                    default:
                        return null;
                }
            }

            internal int? AsInt32()
            {
                var value = AsDouble();
                if (value.HasValue)
                {
                    var intValue = unchecked((int)Math.Round(value.Value));
                    if (intValue == value.Value)
                    {
                        return intValue;
                    }
                }

                return null;
            }

            internal string? AsString()
            {
                switch (_wrapped.ValueKind)
                {
                    case JsonValueKind.String:
                        return _wrapped.GetString();
                    default:
                        return null;
                }
            }

            internal GenericDataObject? ToGenericDataObject()
            {
                switch (Kind)
                {
                    case JsonValueKind.Object:

                        var obj = AsObject()!.Value;
                        var dict = new Dictionary<string, GenericDataObject?>();
                        foreach (var property in obj)
                        {
                            dict.Add(property.Key, property.Value.ToGenericDataObject());
                        }

                        return GenericDataMap.Create(dict);
                    case JsonValueKind.Array:
                        return GenericDataList.Create(AsArray()!.Value.Select<GenericDataObject?>(elem => elem.ToGenericDataObject()));
                    case JsonValueKind.String:
                        return GenericDataString.Create(AsString()!);
                    case JsonValueKind.Number:
                        return GenericDataNumber.Create(AsDouble()!.Value);
                    case JsonValueKind.True:
                        return GenericDataBool.True;
                    case JsonValueKind.False:
                        return GenericDataBool.False;
                    case JsonValueKind.Null:
                        return null;
                    default:
                        throw Exceptions.Unreachable;
                }
            }
        }
    }
}