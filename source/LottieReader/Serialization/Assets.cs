﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Asset ParseAsset(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartObject);

            int e = 0;
            string id = null;
            double width = 0.0;
            double height = 0.0;
            string imagePath = null;
            string fileName = null;
            Layer[] layers = null;

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
                                case "e":
                                    // TODO: unknown what this is. It shows up in image assets.
                                    e = ParseInt(reader);
                                    break;
                                case "h":
                                    height = ParseDouble(reader);
                                    break;
                                case "id":
                                    // Older lotties use a string. New lotties use an int. Handle either as strings.
                                    switch (reader.TokenType)
                                    {
                                        case JsonToken.String:
                                            id = (string)reader.Value;
                                            break;
                                        case JsonToken.Integer:
                                            id = ParseInt(reader).ToString();
                                            break;
                                        default:
                                            throw UnexpectedTokenException(reader);
                                    }

                                    break;
                                case "layers":
                                    layers = ParseLayers(reader).ToArray();
                                    break;
                                case "p":
                                    fileName = (string)reader.Value;
                                    break;
                                case "u":
                                    imagePath = (string)reader.Value;
                                    break;
                                case "w":
                                    width = ParseDouble(reader);
                                    break;

                                // Report but ignore unexpected fields.
                                case "xt":
                                case "nm":
                                default:
                                    _issues.UnexpectedField(currentProperty);
                                    reader.Skip();
                                    break;
                            }
                        }

                        break;
                    case JsonToken.EndObject:
                        {
                            if (id is null)
                            {
                                throw Exception("Asset with no id", reader);
                            }

                            if (layers is object)
                            {
                                return new LayerCollectionAsset(id, new LayerCollection(layers));
                            }
                            else if (imagePath != null && fileName != null)
                            {
                                return CreateImageAsset(id, width, height, imagePath, fileName);
                            }
                            else
                            {
                                _issues.AssetType("NaN");
                                return null;
                            }
                        }

                    default: throw UnexpectedTokenException(reader);
                }
            }

            throw EofException;
        }

        static ImageAsset CreateImageAsset(string id, double width, double height, string imagePath, string fileName)
        {
            // Colon is never valid for a file name. If fileName contains a colon it is probably a URL.
            var colonIndex = fileName.IndexOf(':');
            return string.IsNullOrWhiteSpace(imagePath) && colonIndex > 0
                ? (ImageAsset)CreateEmbeddedImageAsset(id, width, height, dataUrl: fileName)
                : new ExternalImageAsset(id, width, height, imagePath, fileName);
        }

        static EmbeddedImageAsset CreateEmbeddedImageAsset(string id, double width, double height, string dataUrl)
        {
            var colonIndex = dataUrl.IndexOf(':');
            var urlScheme = dataUrl.Substring(0, colonIndex);
            switch (urlScheme)
            {
                case "data":
                    // We only support the data: scheme
                    break;
                default:
                    throw new LottieCompositionReaderException($"Unsupported image asset url scheme: \"{urlScheme}\".");
            }

            // The mime type follows the colon, up to the first comma.
            var commaIndex = dataUrl.IndexOf(',', colonIndex + 1);
            if (commaIndex <= 0)
            {
                throw new LottieCompositionReaderException("Missing image asset url mime type.");
            }

            var (type, subtype, parameters) = ParseMimeType(dataUrl.Substring(colonIndex + 1, commaIndex - colonIndex - 1));

            if (type != "image")
            {
                throw new LottieCompositionReaderException($"Unsupported mime type: \"{type}\".");
            }

            switch (subtype)
            {
                case "png":
                case "jpg":
                    break;

                case "jpeg":
                    // Standardize on "jpg" for the convenience of consumers.
                    subtype = "jpg";
                    break;

                default:
                    throw new LottieCompositionReaderException($"Unsupported mime image subtype: \"{subtype}\".");
            }

            // The embedded data starts after the comma.
            var bytes = Convert.FromBase64String(dataUrl.Substring(commaIndex + 1));

            return new EmbeddedImageAsset(id, width, height, bytes, subtype);
        }

        static (string type, string subtype, string[] parameter) ParseMimeType(string mimeType)
        {
            var typeAndParameters = mimeType.Split(';').Select(s => s.Trim()).ToArray();
            var typeAndSubtype = typeAndParameters[0].Split('/');
            var parameters = typeAndParameters.Skip(1).ToArray();
            return (typeAndSubtype[0], typeAndSubtype.Length > 1 ? typeAndSubtype[1] : string.Empty, parameters);
        }
    }
}