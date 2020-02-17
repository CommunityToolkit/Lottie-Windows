// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json.Linq;

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
            readonly JToken _wrapped;

            internal LottieJsonElement(LottieCompositionReader owner, JToken wrapped)
            {
                if (wrapped is null)
                {
                    throw new ArgumentException();
                }

                _owner = owner;
                _wrapped = wrapped;
            }

            internal LottieJsonArrayElement? AsArray()
                => _wrapped.Type == JTokenType.Array
                    ? new LottieJsonArrayElement(_owner, _wrapped)
                    : (LottieJsonArrayElement?)null;

            internal LottieJsonObjectElement? AsObject()
            => _wrapped.Type == JTokenType.Object
                ? new LottieJsonObjectElement(_owner, _wrapped)
                : (LottieJsonObjectElement?)null;

            internal JTokenType Type => _wrapped.Type;

            internal bool? AsBoolean()
            {
                switch (_wrapped.Type)
                {
                    case JTokenType.Boolean:
                        return (bool)_wrapped;
                    default:
                        var number = AsDouble();
                        return number.HasValue
                            ? number != 0
                            : (bool?)null;
                }
            }

            internal double? AsDouble()
            {
                switch (_wrapped.Type)
                {
                    case JTokenType.Float:
                    case JTokenType.Integer:
                        return (double)_wrapped;
                    case JTokenType.String:
                        return double.TryParse((string)_wrapped, out var result)
                            ? result
                            : (double?)null;

                    case JTokenType.Array:
                        {
                            var array = AsArray().Value;
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

            internal string AsString()
            {
                switch (_wrapped.Type)
                {
                    case JTokenType.String:
                        return (string)_wrapped;
                    default:
                        return null;
                }
            }

            internal double GetDouble()
            {
                var value = AsDouble();
                if (value.HasValue)
                {
                    return value.Value;
                }
                else
                {
                    throw LottieCompositionReader.UnexpectedTokenException(Type);
                }
            }
        }
    }
}