// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Char ParseChar(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartObject);

            string ch = null;
            string fFamily = null;
            double? size = null;
            string style = null;
            double? width = null;
            List<ShapeLayerContent> shapes = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        {
                            var currentProperty = (string)reader.Value;
                            ConsumeToken(reader);

                            switch (currentProperty)
                            {
                                case "ch":
                                    ch = (string)reader.Value;
                                    break;
                                case "data":
                                    shapes = ReadShapes(JCObject.Load(reader, s_jsonLoadSettings));
                                    break;
                                case "fFamily":
                                    fFamily = (string)reader.Value;
                                    break;
                                case "size":
                                    size = ParseDouble(reader);
                                    break;
                                case "style":
                                    style = (string)reader.Value;
                                    break;
                                case "w":
                                    width = ParseDouble(reader);
                                    break;
                                default:
                                    _issues.UnexpectedField(currentProperty);
                                    break;
                            }
                        }

                        break;
                    case JsonToken.EndObject:
                        {
                            return new Char(ch, fFamily, style, size ?? 0, width ?? 0, shapes);
                        }

                    default: throw UnexpectedTokenException(reader);
                }
            }

            throw EofException;
        }
    }
}