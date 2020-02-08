// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        static bool ParseBool(ref Reader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return reader.GetInt64() != 0;
                case JsonToken.Float:
                    return reader.GetDouble() != 0;
                case JsonToken.Boolean:
                    return reader.GetBoolean();
                default:
                    throw Exception($"Expected a bool, but got {reader.TokenType}", ref reader);
            }
        }

        static double ParseDouble(ref Reader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return reader.GetInt64();
                case JsonToken.Float:
                    return reader.GetDouble();
                case JsonToken.String:
                    if (double.TryParse(reader.GetString(), out var result))
                    {
                        return result;
                    }

                    break;
            }

            throw Exception($"Expected a number, but got {reader.TokenType}", ref reader);
        }

        static int ParseInt(ref Reader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return checked((int)reader.GetInt64());
                case JsonToken.Float:
                    return checked((int)(long)Math.Round(reader.GetDouble()));
                case JsonToken.String:
                    if (double.TryParse(reader.GetString(), out var result))
                    {
                        return checked((int)(long)Math.Round(result));
                    }

                    break;
            }

            throw Exception($"Expected a number, but got {reader.TokenType}", ref reader);
        }

        static bool? ReadBool(JCObject obj, string name)
        {
            if (!obj.ContainsKey(name))
            {
                return null;
            }

            var value = obj.GetNamedValue(name);

            switch (value.Type)
            {
                case JTokenType.Boolean:
                    return obj.GetNamedBoolean(name);
                case JTokenType.Integer:
                case JTokenType.Float:
                    return ReadInt(obj, name)?.Equals(1);
                case JTokenType.Null:
                    // Treat a missing value as false.
                    return false;
                case JTokenType.String:
                case JTokenType.Array:
                case JTokenType.Object:
                default:
                    throw UnexpectedTokenException(value.Type);
            }
        }

        static double ReadFloat(JToken jsonValue)
        {
            switch (jsonValue.Type)
            {
                case JTokenType.Float:
                case JTokenType.Integer:
                    return (double)jsonValue;
                case JTokenType.Array:
                    {
                        var array = jsonValue.AsArray();
                        switch (array.Count)
                        {
                            case 0:
                                throw UnexpectedTokenException(jsonValue.Type);
                            case 1:
                                return (double)array[0];
                            default:
                                // Some Lottie files have multiple values in arrays that should only have one. Just
                                // take the first value.
                                return (double)array[0];
                        }
                    }

                case JTokenType.Null:
                    // Treat a missing value as 0.
                    return 0.0;

                case JTokenType.Boolean:
                case JTokenType.String:
                case JTokenType.Object:
                default:
                    throw UnexpectedTokenException(jsonValue.Type);
            }
        }

        static int? ReadInt(JCObject obj, string name)
        {
            var value = obj.GetNamedNumber(name, double.NaN);
            if (double.IsNaN(value))
            {
                return null;
            }

            // Newtonsoft has its own casting logic so to bypass this, we first cast to a double and then round
            // because the desired behavior is to round doubles to the nearest value.
            var intValue = unchecked((int)Math.Round((double)value));
            if (value != intValue)
            {
                return null;
            }

            return intValue;
        }

        static Vector2[] ReadVector2Array(JCArray array)
        {
            IEnumerable<Vector2> ToVector2Enumerable()
            {
                var count = array.Count;
                for (int i = 0; i < count; i++)
                {
                    yield return ReadVector2FromJsonArray(array[i].AsArray());
                }
            }

            return ToVector2Enumerable().ToArray();
        }

        static Vector2 ReadVector2FromJsonArray(JCArray array)
        {
            double x = 0;
            double y = 0;
            int i = 0;
            var count = array.Count;
            for (; i < count; i++)
            {
                // NOTE: indexing JsonArray is faster than enumerating it.
                var number = (double)array[i];
                switch (i)
                {
                    case 0:
                        x = number;
                        break;
                    case 1:
                        y = number;
                        break;
                }
            }

            // Allow any number of values to be specified. Assume 0 for any missing values.
            return new Vector2(x, y);
        }

        static Vector3 ReadVector3(JToken jsonValue) => ReadVector3FromJsonArray(jsonValue.AsArray());

        static Vector3 ReadVector3FromJsonArray(JCArray array)
        {
            double x = 0;
            double y = 0;
            double z = 0;
            int i = 0;
            var count = array.Count;
            for (; i < count; i++)
            {
                // NOTE: indexing JsonArray is faster than enumerating it.
                var number = (double)array[i];
                switch (i)
                {
                    case 0:
                        x = number;
                        break;
                    case 1:
                        y = number;
                        break;
                    case 2:
                        z = number;
                        break;
                }
            }

            // Allow any number of values to be specified. Assume 0 for any missing values.
            return new Vector3(x, y, z);
        }
    }
}