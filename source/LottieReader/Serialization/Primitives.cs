// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;

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

        static Vector2[] ReadVector2Array(in LottieJsonArrayElement array)
        {
            var len = array.Count;
            var result = new Vector2[len];
            for (var i = 0; i < len; i++)
            {
                result[i] = ReadVector2FromJsonArray(array[i].AsArray().Value);
            }

            return result;
        }

        static Vector2 ReadVector2FromJsonArray(in LottieJsonArrayElement array)
        {
            double x = 0;
            double y = 0;
            int i = 0;
            var count = array.Count;
            for (; i < count; i++)
            {
                var number = array[i].AsDouble() ?? 0;
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

        static Vector3 ReadVector3(in LottieJsonObjectElement obj)
        {
            var x = obj.DoubleOrNullProperty("x");
            var y = obj.DoubleOrNullProperty("y");
            var z = obj.DoubleOrNullProperty("z");
            return new Vector3(x ?? 0, y ?? 0, z ?? 0);
        }

        static Vector3 ReadVector3(in LottieJsonElement jsonValue)
            => ReadVector3FromJsonArray(jsonValue.AsArray().Value);

        static Vector3 ReadVector3FromJsonArray(in LottieJsonArrayElement? array)
            => array.HasValue
                ? ReadVector3FromJsonArray(array.Value)
                : default(Vector3);

        static Vector3 ReadVector3FromJsonArray(in LottieJsonArrayElement array)
        {
            double x = 0;
            double y = 0;
            double z = 0;
            int i = 0;
            var count = array.Count;
            for (; i < count; i++)
            {
                // NOTE: indexing JsonArray is faster than enumerating it.
                var number = array[i].AsDouble() ?? 0;
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