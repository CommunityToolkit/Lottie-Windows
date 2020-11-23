// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

// Uncomment this to give each element a unique name. This is useful
// for debugging how an element gets translated.
//#define UniqueifyNames
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    // See: https://github.com/airbnb/lottie-web/tree/master/docs/json for the (usually out-of-date) schema.
    // See: https://helpx.adobe.com/pdf/after_effects_reference.pdf for the After Effects semantics.
#if PUBLIC
    public
#endif
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        readonly ParsingIssues _issues;
        readonly Options _options;
#if UniqueifyNames
        // The names discovered so far, and their counts.
        // This is used to give each element a unique name. This helps
        // when trying to debug how an element gets translated.
        readonly Dictionary<string, int> _names = new Dictionary<string, int>();
#endif // UniqueifyNames

        /// <summary>
        /// Specifies optional behavior for the reader.
        /// </summary>
        [Flags]
        public enum Options
        {
            None = 0,

            /// <summary>
            /// Do not ignore the alpha channel when reading color values from arrays.
            /// </summary>
            /// <description>
            /// Lottie files produced by BodyMovin include an alpha channel value that
            /// is ignored by renderers. By default the <see cref="LottieCompositionReader" />
            /// will set the alpha channel to 1.0. By enabling this option the alpha channel
            /// will be set to whatever is in the Lottie file. This option does not apply to
            /// color values read from hex strings.
            /// </description>
            DoNotIgnoreAlpha = 1,

            /// <summary>
            /// Do not read the Name values.
            /// </summary>
            IgnoreNames = 2,

            /// <summary>
            /// Do not read the Match Name values.
            /// </summary>
            IgnoreMatchNames = 4,
        }

        /// <summary>
        /// Parses a Lottie file to create a <see cref="LottieComposition"/>.
        /// </summary>
        /// <returns>A <see cref="LottieComposition"/> read from the json stream.</returns>
        public static LottieComposition? ReadLottieCompositionFromJsonStream(
            Stream stream,
            Options options,
            out IReadOnlyList<(string Code, string Description)> issues)
        {
            ReadStreamToUTF8(stream, out var utf8Text);
            return ReadLottieCompositionFromJson(utf8Text, options, out issues);
        }

        LottieCompositionReader(Options options)
        {
            _issues = new ParsingIssues(throwOnIssue: false);
            _options = options;
            _animatableColorParser = new AnimatableColorParser(!options.HasFlag(Options.DoNotIgnoreAlpha));
        }

        static LottieComposition? ReadLottieCompositionFromJson(
            in ReadOnlySpan<byte> utf8JsonText,
            Options options,
            out IReadOnlyList<(string Code, string Description)> issues)
        {
            var reader = new LottieCompositionReader(options);
            var jsonReader = new Reader(reader, utf8JsonText);

            LottieComposition? result = null;
            try
            {
                result = reader.ParseLottieComposition(ref jsonReader);
            }
            catch (LottieCompositionReaderException e)
            {
                reader._issues.FatalError(e.Message);
            }

            issues = reader._issues.GetIssues();
            return result;
        }

        LottieComposition ParseLottieComposition(ref Reader reader)
        {
            string? version = null;
            double? framesPerSecond = null;
            double? inPoint = null;
            double? outPoint = null;
            double? width = null;
            double? height = null;
            string? name = null;
            bool? is3d = null;
            var assets = Array.Empty<Asset>();
            var chars = Array.Empty<Char>();
            var fonts = Array.Empty<Font>();
            var layers = Array.Empty<Layer>();
            var markers = Array.Empty<Marker>();
            Dictionary<string, GenericDataObject?>? extraData = null;

            try
            {
                reader.ConsumeToken();

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.Comment:
                            // Ignore comments.
                            reader.ConsumeToken();
                            break;

                        case JsonTokenType.PropertyName:
                            var currentProperty = reader.GetString();

                            reader.ConsumeToken();

                            switch (currentProperty)
                            {
                                case "assets":
                                    assets = ParseArray(ref reader, ParseAsset);
                                    break;
                                case "chars":
                                    chars = ParseArray(ref reader, ParseChar);
                                    break;
                                case "ddd":
                                    is3d = reader.ParseBool();
                                    break;
                                case "fr":
                                    framesPerSecond = reader.ParseDouble();
                                    break;
                                case "fonts":
                                    fonts = ParseFonts(ref reader);
                                    break;
                                case "layers":
                                    layers = ParseArray(ref reader, ParseLayer);
                                    break;
                                case "h":
                                    height = reader.ParseDouble();
                                    break;
                                case "ip":
                                    inPoint = reader.ParseDouble();
                                    break;
                                case "op":
                                    outPoint = reader.ParseDouble();
                                    break;
                                case "markers":
                                    markers = ParseArray(ref reader, ParseMarker);
                                    break;
                                case "nm":
                                    name = reader.GetString();
                                    break;
                                case "v":
                                    version = reader.GetString();
                                    break;
                                case "w":
                                    width = reader.ParseDouble();
                                    break;

                                // Treat any other property as an extension of the BodyMovin format.
                                default:
                                    {
                                        // Report the extension as an issue, unless it is a well-known
                                        // extension to the BodyMovin format.
                                        switch (currentProperty)
                                        {
                                            // "meta" is an extension created by the LottieFiles.com Lottie
                                            // plugin.
                                            case "meta":
                                                break;

                                            default:
                                                _issues.UnexpectedField(currentProperty);
                                                break;
                                        }

                                        if (extraData is null)
                                        {
                                            extraData = new Dictionary<string, GenericDataObject?>();
                                        }

                                        using var subDocument = reader.ParseElement();
                                        var subDocumentAsGenericDataObject = subDocument.RootElement.ToGenericDataObject();
                                        if (!(subDocumentAsGenericDataObject is null))
                                        {
                                            extraData.Add(currentProperty, subDocumentAsGenericDataObject);
                                        }
                                    }

                                    break;
                            }

                            break;

                        case JsonTokenType.EndObject:
                            {
                                // Check that the required properties were found. If any are missing, throw.
                                if (version is null)
                                {
                                    throw reader.Throw("Version parameter not found.");
                                }

                                if (!width.HasValue)
                                {
                                    throw reader.Throw("Width parameter not found.");
                                }

                                if (!height.HasValue)
                                {
                                    throw reader.Throw("Height parameter not found.");
                                }

                                if (!inPoint.HasValue)
                                {
                                    throw reader.Throw("Start frame parameter not found.");
                                }

                                if (!outPoint.HasValue)
                                {
                                    throw reader.Throw("End frame parameter not found.");
                                }

                                if (layers is null)
                                {
                                    throw reader.Throw("No layers found.");
                                }

                                int[] versions;
                                try
                                {
                                    versions = version.Split('.').Select(int.Parse).ToArray();
                                }
                                catch (FormatException)
                                {
                                    // Ignore
                                    versions = new[] { 0, 0, 0 };
                                }
                                catch (OverflowException)
                                {
                                    // Ignore
                                    versions = new[] { 0, 0, 0 };
                                }

                                var result = new LottieComposition(
                                                    name: name ?? string.Empty,
                                                    width: width ?? 0.0,
                                                    height: height ?? 0.0,
                                                    inPoint: inPoint ?? 0.0,
                                                    outPoint: outPoint ?? 0.0,
                                                    framesPerSecond: framesPerSecond ?? 0.0,
                                                    is3d: false,
                                                    version: new Version(versions[0], versions[1], versions[2]),
                                                    assets: new AssetCollection(assets),
                                                    chars: chars,
                                                    extraData: extraData is null ? GenericDataMap.Empty : GenericDataMap.Create(extraData),
                                                    fonts: fonts,
                                                    layers: new LayerCollection(layers),
                                                    markers: markers);
                                return result;
                            }

                        // Here means the JSON was invalid or our parser got confused. There is no way to
                        // recover from this, so throw.
                        default:
                            throw reader.ThrowUnexpectedToken();
                    }
                }
            }
            catch (JsonException e)
            {
                // Re-throw errors from the JSON parser using our own exception.
                throw ReaderException(e.Message);
            }

            throw EofException;
        }

        string ReadName(in LottieJsonObjectElement obj)
        {
            if (_options.HasFlag(Options.IgnoreNames))
            {
                obj.IgnorePropertyIntentionally("nm");
                return string.Empty;
            }
            else
            {
                var result = obj.StringPropertyOrNull("nm") ?? string.Empty;

#if UniqueifyNames
                if (!_names.TryGetValue(result, out var count))
                {
                    count = 0;
                    _names.Add(result, count);
                }

                count++;
                _names[result] = count;
                result = $"{result} #{count:000}";
#endif // UniqueifyNames

                return result;
            }
        }

        string ReadMatchName(in LottieJsonObjectElement obj)
        {
            if (_options.HasFlag(Options.IgnoreMatchNames))
            {
                obj.IgnorePropertyIntentionally("mn");
                return string.Empty;
            }
            else
            {
                return obj.StringPropertyOrNull("mn") ?? string.Empty;
            }
        }

        static PathGeometry ParseGeometry(in LottieJsonElement element)
        {
            LottieJsonObjectElement? pointsData = null;
            if (element.Kind == JsonValueKind.Array)
            {
                var firstItem = element.AsArray()?[0];
                var firstItemAsObject = firstItem?.AsObject();
                if (firstItemAsObject?.ContainsProperty("v") == true)
                {
                    pointsData = firstItemAsObject;
                }
            }
            else if (element.AsObject()?.ContainsProperty("v") == true)
            {
                pointsData = element.AsObject();
            }

            if (pointsData is null)
            {
                return new PathGeometry(new Sequence<BezierSegment>(Array.Empty<BezierSegment>(), takeOwnership: true), isClosed: false);
            }

            var points = pointsData.Value;

            var vertices = points.ArrayPropertyOrNull("v");
            var inTangents = points.ArrayPropertyOrNull("i");
            var outTangents = points.ArrayPropertyOrNull("o");
            var isClosed = points.BoolPropertyOrNull("c") ?? false;

            if (vertices is null || inTangents is null || outTangents is null)
            {
                throw ReaderException($"Unable to process points array or tangents. {points}");
            }

            var beziers = new BezierSegment[isClosed ? vertices.Value.Count : Math.Max(vertices.Value.Count - 1, 0)];

            if (beziers.Length > 0)
            {
                // The vertices for the figure.
                var verticesAsVector2 = ReadArrayOfVector2(vertices.Value);

                // The control points that define the cubic Beziers between the vertices.
                var inTangentsAsVector2 = ReadArrayOfVector2(inTangents.Value);
                var outTangentsAsVector2 = ReadArrayOfVector2(outTangents.Value);

                if (verticesAsVector2.Length != inTangentsAsVector2.Length ||
                    verticesAsVector2.Length != outTangentsAsVector2.Length)
                {
                    throw ReaderException($"Invalid path data. {points}");
                }

                var cp3 = verticesAsVector2[0];

                for (var i = 0; i < beziers.Length; i++)
                {
                    // cp0 is the start point of the segment.
                    var cp0 = cp3;

                    // cp1 is relative to cp0
                    var cp1 = cp0 + outTangentsAsVector2[i];

                    // cp3 is the endpoint of the segment.
                    cp3 = verticesAsVector2[(i + 1) % verticesAsVector2.Length];

                    // cp2 is relative to cp3
                    var cp2 = cp3 + inTangentsAsVector2[(i + 1) % inTangentsAsVector2.Length];

                    beziers[i] = new BezierSegment(
                        cp0: cp0,
                        cp1: cp1,
                        cp2: cp2,
                        cp3: cp3);
                }
            }

            return new PathGeometry(new Sequence<BezierSegment>(beziers, takeOwnership: true), isClosed);
        }

        static Vector2[] ReadArrayOfVector2(in LottieJsonArrayElement array)
        {
            var len = array.Count;
            var result = new Vector2[len];
            for (var i = 0; i < len; i++)
            {
                result[i] = array[i].AsArray()?.AsVector2() ?? default(Vector2);
            }

            return result;
        }

        static Opacity ParseOpacity(in LottieJsonElement jsonValue) => Opacity.FromPercent(jsonValue.AsDouble() ?? 0);

        static Opacity ParseOpacityByte(in LottieJsonElement jsonValue) => Opacity.FromByte(jsonValue.AsDouble() ?? 0);

        static Rotation ParseRotation(in LottieJsonElement jsonValue) => Rotation.FromDegrees(jsonValue.AsDouble() ?? 0);

        static Trim ParseTrim(in LottieJsonElement jsonValue) => Trim.FromPercent(jsonValue.AsDouble() ?? 0);

        static Vector3 ParseVector3(in LottieJsonElement jsonValue)
        {
            return jsonValue.Kind switch
            {
                JsonValueKind.Object => jsonValue.AsObject()?.AsVector3() ?? Vector3.Zero,
                JsonValueKind.Array => jsonValue.AsArray()?.AsVector3() ?? Vector3.Zero,
                _ => default(Vector3),
            };
        }

        delegate T Parser<T>(ref Reader reader);

        T[] ParseArray<T>(ref Reader reader, Parser<T?> parser)
            where T : class
        {
            reader.ExpectToken(JsonTokenType.StartArray);

            ArrayBuilder<T> result = default;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        result.AddItemIfNotNull(parser(ref reader));
                        break;

                    case JsonTokenType.EndArray:
                        return result.ToArray();

                    default:
                        throw reader.ThrowUnexpectedToken();
                }
            }

            throw EofException;
        }

        static void ReadStreamToUTF8(Stream stream, out ReadOnlySpan<byte> utf8Text)
        {
            // This buffer size is chosen to be about 50% larger than
            // the average file size in our corpus, so most of the time
            // we don't need to reallocate and copy.
            var buffer = new byte[150000];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            var spaceLeftInBuffer = buffer.Length - bytesRead;

            while (spaceLeftInBuffer == 0)
            {
                // Might be more to read. Expand the buffer.
                var newBuffer = new byte[buffer.Length * 2];
                spaceLeftInBuffer = buffer.Length;
                var totalBytesRead = buffer.Length;
                Array.Copy(buffer, 0, newBuffer, 0, totalBytesRead);
                buffer = newBuffer;
                bytesRead = stream.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                spaceLeftInBuffer -= bytesRead;
            }

            utf8Text = new ReadOnlySpan<byte>(buffer);
            NormalizeTextToUTF8(ref utf8Text);
        }

        // Updates the given span so that its contents are UTF8.
        static void NormalizeTextToUTF8(ref ReadOnlySpan<byte> text)
        {
            if (text.Length >= 1)
            {
                switch (text[0])
                {
                    case 0xEF:
                        // Possibly start of UTF8 BOM.
                        if (text.Length >= 3 && text[1] == 0xBB && text[2] == 0xBF)
                        {
                            // UTF8 BOM.
                            // Step over the UTF8 BOM.
                            text = text.Slice(3, text.Length - 3);
                        }

                        break;
                    case 0xFE:
                        // Possibly start of UTF16LE BOM.
                        if (text.Length >= 2 && text[1] == 0xFF)
                        {
                            // Step over the UTF16 BOM and convert to UTF8.
                            text = Encoding.UTF8.GetBytes(
                                                Encoding.Unicode.GetString(
                                                    text.Slice(2, text.Length - 2)
#if WINDOWS_UWP
                            // NOTE: the ToArray here is necessary for UWP apps as they don't
                            //       yet support GetString(ReadOnlySpan<byte>).
                                                    .ToArray()
#endif // WINDOWS_UWP
                                                    ));
                        }

                        break;
                    case 0xFF:
                        // Possibly start of UTF16BE BOM.
                        if (text.Length >= 2 && text[1] == 0xFE)
                        {
                            // Step over the UTF16 BOM and convert to UTF8.
                            text = Encoding.UTF8.GetBytes(
                                                Encoding.BigEndianUnicode.GetString(
                                                    text.Slice(2, text.Length - 2)
#if WINDOWS_UWP
                            // NOTE: the ToArray here is necessary for UWP apps as they don't
                            //       yet support GetString(ReadOnlySpan<byte>).
                                                    .ToArray()
#endif // WINDOWS_UWP
                                                    ));
                        }

                        break;
                }
            }
        }

        // We got to the end of the file while still reading. Fatal.
        static LottieCompositionReaderException EofException => ReaderException("EOF");

        static LottieCompositionReaderException ReaderException(string message) => new LottieCompositionReaderException(message);
    }
}
