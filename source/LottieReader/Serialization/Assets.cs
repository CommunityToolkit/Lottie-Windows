// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Linq;
using System.Text.Json;

namespace CommunityToolkit.WinUI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Asset? ParseAsset(ref Reader reader)
        {
            reader.ExpectToken(JsonTokenType.StartObject);

            int e = 0;
            string? id = null;
            double width = 0.0;
            double height = 0.0;
            string? imagePath = null;
            string? fileName = null;
            Layer[]? layers = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        {
                            var currentProperty = reader.GetString();
                            reader.ConsumeToken();

                            switch (currentProperty)
                            {
                                case "e":
                                    // TODO: unknown what this is. It shows up in image assets.
                                    e = reader.ParseInt();
                                    break;
                                case "h":
                                    height = reader.ParseDouble();
                                    break;
                                case "id":
                                    // Older lotties use a string. New lotties use an int. Handle either as strings.
                                    id = reader.TokenType switch
                                    {
                                        JsonTokenType.String => reader.GetString(),
                                        JsonTokenType.Number => reader.ParseInt().ToString(),
                                        _ => throw reader.ThrowUnexpectedToken(),
                                    };
                                    break;
                                case "layers":
                                    layers = ParseArray(ref reader, ParseLayer);
                                    break;
                                case "p":
                                    fileName = reader.GetString();
                                    break;
                                case "u":
                                    imagePath = reader.GetString();
                                    break;
                                case "w":
                                    width = reader.ParseDouble();
                                    break;

                                case "nm": // Name - rare property, ignore
                                    reader.Skip();
                                    break;
                                case "fr": // Framerate - rare property. Report it with details anyway.
                                    _issues.UnexpectedFieldValueInfo(currentProperty, reader.ParseDouble().ToString(), "framerate");
                                    break;

                                // Report but ignore unexpected properties.
                                case "xt":
                                default:
                                    _issues.UnexpectedField(currentProperty);
                                    reader.Skip();
                                    break;
                            }
                        }

                        break;
                    case JsonTokenType.EndObject:
                        {
                            if (id is null)
                            {
                                throw reader.Throw("Asset with no id");
                            }

                            if (layers is object)
                            {
                                return new LayerCollectionAsset(id, new LayerCollection(layers));
                            }
                            else if (imagePath is not null && fileName is not null)
                            {
                                return CreateImageAsset(id, width, height, imagePath, fileName);
                            }
                            else
                            {
                                _issues.AssetType("NaN");
                                return null;
                            }
                        }

                    default: throw reader.ThrowUnexpectedToken();
                }
            }

            throw EofException;
        }

        static ImageAsset CreateImageAsset(string id, double width, double height, string imagePath, string fileName)
        {
            // Colon is never valid for a file name. Any embedded file will have a colon to delimit
            // the url scheme.
            var colonIndex = fileName.IndexOf(':');
            return colonIndex > 0
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
                    throw ReaderException($"Unsupported image asset url scheme: \"{urlScheme}\".");
            }

            // The mime type follows the colon, up to the first comma.
            var commaIndex = dataUrl.IndexOf(',', colonIndex + 1);
            if (commaIndex <= 0)
            {
                throw ReaderException("Missing image asset url mime type.");
            }

            var (type, subtype, parameters) = ParseMimeType(dataUrl.Substring(colonIndex + 1, commaIndex - colonIndex - 1));

            if (type != "image")
            {
                throw ReaderException($"Unsupported mime type: \"{type}\".");
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
                    throw ReaderException($"Unsupported mime image subtype: \"{subtype}\".");
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