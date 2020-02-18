// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Char ParseChar(ref Reader reader)
        {
            ExpectToken(ref reader, JsonTokenType.StartObject);

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
                    case JsonTokenType.PropertyName:
                        {
                            var currentProperty = reader.GetString();
                            ConsumeToken(ref reader);

                            switch (currentProperty)
                            {
                                case "ch":
                                    ch = reader.GetString();
                                    break;
                                case "data":
                                    shapes = ReadShapes(LottieJsonObjectElement.Load(this, ref reader, s_jsonLoadSettings));
                                    break;
                                case "fFamily":
                                    fFamily = reader.GetString();
                                    break;
                                case "size":
                                    size = reader.ParseDouble();
                                    break;
                                case "style":
                                    style = reader.GetString();
                                    break;
                                case "w":
                                    width = reader.ParseDouble();
                                    break;
                                default:
                                    _issues.UnexpectedField(currentProperty);
                                    break;
                            }
                        }

                        break;
                    case JsonTokenType.EndObject:
                        {
                            return new Char(ch, fFamily, style, size ?? 0, width ?? 0, shapes);
                        }

                    default: throw UnexpectedTokenException(ref reader);
                }
            }

            throw EofException;
        }
    }
}