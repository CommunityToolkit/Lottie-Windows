// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Newtonsoft.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    // A type that has a similar interface to System.Text.Json.Utf8JsonReader.
    // This type helps to hide the choice of Newtonsoft.Json vs System.Text.Json.
    ref struct Reader
    {
        readonly JsonReader _reader;

        internal Reader(JsonReader reader)
        {
            _reader = reader;
        }

        internal JsonTokenType TokenType
        {
            get
            {
                switch (_reader.TokenType)
                {
                    case JsonToken.None:
                        return JsonTokenType.None;
                    case JsonToken.StartObject:
                        return JsonTokenType.StartObject;
                    case JsonToken.StartArray:
                        return JsonTokenType.StartArray;
                    case JsonToken.PropertyName:
                        return JsonTokenType.PropertyName;
                    case JsonToken.Comment:
                        return JsonTokenType.Comment;
                    case JsonToken.Integer:
                    case JsonToken.Float:
                        return JsonTokenType.Number;
                    case JsonToken.String:
                        return JsonTokenType.String;
                    case JsonToken.Boolean:
                        return JsonTokenType.False;
                    case JsonToken.Null:
                        return JsonTokenType.Null;
                    case JsonToken.EndObject:
                        return JsonTokenType.EndObject;
                    case JsonToken.EndArray:
                        return JsonTokenType.EndArray;
                    case JsonToken.Bytes:
                    case JsonToken.Date:
                    case JsonToken.EndConstructor:
                    case JsonToken.Raw:
                    case JsonToken.StartConstructor:
                    case JsonToken.Undefined:
                    default:
                        throw Exceptions.Unreachable;
                }
            }
        }

        internal bool Read() => _reader.Read();

        internal void Skip() => _reader.Skip();

        internal bool GetBoolean() => (bool)_reader.Value;

        internal double GetDouble() => (double)_reader.Value;

        internal long GetInt64() => (long)_reader.Value;

        internal string GetString() => (string)_reader.Value;

        internal bool ParseBool()
        {
            switch (_reader.TokenType)
            {
                case JsonToken.Integer:
                    return GetInt64() != 0;
                case JsonToken.Float:
                    return GetDouble() != 0;
                case JsonToken.Boolean:
                    return GetBoolean();
                default:
                    throw new LottieCompositionReaderException($"Expected a bool, but got {_reader.TokenType}");
            }
        }

        internal double ParseDouble()
        {
            switch (_reader.TokenType)
            {
                case JsonToken.Integer:
                    return GetInt64();
                case JsonToken.Float:
                    return GetDouble();
                case JsonToken.String:
                    if (double.TryParse(GetString(), out var result))
                    {
                        return result;
                    }

                    break;
            }

            throw new LottieCompositionReaderException($"Expected a number, but got {_reader.TokenType}");
        }

        internal int ParseInt()
        {
            switch (_reader.TokenType)
            {
                case JsonToken.Integer:
                    return checked((int)GetInt64());
                case JsonToken.Float:
                    return checked((int)(long)Math.Round(GetDouble()));
                case JsonToken.String:
                    if (double.TryParse(GetString(), out var result))
                    {
                        return checked((int)(long)Math.Round(result));
                    }

                    break;
            }

            throw new LottieCompositionReaderException($"Expected a number, but got {_reader.TokenType}");
        }

        // Not part of System.Text.Json. This is a backdoor to get access to the
        // underlying Newtonsoft reader while we transition the code more to
        // System.Text.Json patterns.
        internal JsonReader NewtonsoftReader => _reader;
    }
}