// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        ref struct Reader
        {
            LottieCompositionReader _owner;
            Utf8JsonReader _jsonReader;

            internal Reader(LottieCompositionReader owner, ReadOnlySpan<byte> jsonText)
            {
                _owner = owner;
                _jsonReader = new Utf8JsonReader(
                                    jsonText,
                                    new JsonReaderOptions
                                    {
                                        // Be resilient about trailing commas - ignore them.
                                        AllowTrailingCommas = true,

                                        // Be resilient about comments - ignore them.
                                        CommentHandling = JsonCommentHandling.Skip,

                                        // Fail if the JSON exceeds this depth.
                                        MaxDepth = 64,
                                    });
            }

            internal void ExpectToken(JsonTokenType tokenType)
            {
                if (_jsonReader.TokenType != tokenType)
                {
                    throw ThrowUnexpectedToken(tokenType);
                }
            }

            internal JsonTokenType TokenType => _jsonReader.TokenType;

            internal bool Read() => _jsonReader.Read();

            // Consumes a token from the stream.
            internal void ConsumeToken()
            {
                if (!_jsonReader.Read())
                {
                    throw EofException;
                }
            }

            internal void Skip() => _jsonReader.Skip();

            internal string GetString() => _jsonReader.GetString();

            internal LottieJsonDocument ParseElement()
                => JsonDocument.TryParseValue(ref _jsonReader, out var document)
                    ? new LottieJsonDocument(_owner, document)
                    : throw Throw("Failed to parse value.");

            internal bool ParseBool()
            {
                switch (_jsonReader.TokenType)
                {
                    case JsonTokenType.Number:
                        return _jsonReader.GetDouble() != 0;
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.False:
                        return false;
                    default:
                        throw ThrowUnexpectedToken("bool");
                }
            }

            internal double ParseDouble()
            {
                switch (_jsonReader.TokenType)
                {
                    case JsonTokenType.Number:
                        return _jsonReader.GetDouble();
                    case JsonTokenType.String:
                        if (double.TryParse(_jsonReader.GetString(), out var result))
                        {
                            return result;
                        }

                        break;
                }

                throw ThrowUnexpectedToken(JsonTokenType.Number);
            }

            internal int ParseInt()
            {
                switch (_jsonReader.TokenType)
                {
                    case JsonTokenType.Number:
                        return checked((int)(long)Math.Round(_jsonReader.GetDouble()));
                    case JsonTokenType.String:
                        if (double.TryParse(_jsonReader.GetString(), out var result))
                        {
                            return checked((int)(long)Math.Round(result));
                        }

                        break;
                }

                throw ThrowUnexpectedToken(JsonTokenType.Number);
            }

            internal Exception ThrowUnexpectedToken(JsonTokenType expectedType) => ThrowUnexpectedToken(expectedType.ToString());

            internal Exception ThrowUnexpectedToken(string expectedType)
                => Throw($"Unexpected token. Expected {expectedType} but got {_jsonReader.TokenType}");

            internal Exception ThrowUnexpectedToken() => Throw($"Unexpected token: {_jsonReader.TokenType}");

            internal Exception Throw(string message)
                => throw Exception($"{message} @ {_jsonReader.Position} depth={_jsonReader.CurrentDepth}");
        }
    }
}