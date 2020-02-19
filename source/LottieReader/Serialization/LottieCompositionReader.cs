// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PathGeometry = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Sequence<Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.BezierSegment>;

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
        static readonly JsonLoadSettings s_jsonLoadSettings = new JsonLoadSettings
        {
            // Ignore commands and line info. Not needed and makes the parser a bit faster.
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Ignore,
        };

        readonly ParsingIssues _issues = new ParsingIssues(throwOnIssue: false);
        Options _options;

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
        public static LottieComposition ReadLottieCompositionFromJsonStream(
            Stream stream,
            Options options,
            out IReadOnlyList<(string Code, string Description)> issues)
        {
            JsonReader jsonReader;
            try
            {
                var streamReader = new StreamReader(stream);
                jsonReader = new JsonTextReader(streamReader);
            }
            catch (Exception e)
            {
                var issueCollector = new ParsingIssues(throwOnIssue: false);
                issueCollector.FailedToParseJson(e.Message);
                issues = issueCollector.GetIssues();
                return null;
            }

            var reader = new Reader(jsonReader);
            return ReadLottieCompositionFromJson(ref reader, options, out issues);
        }

        LottieCompositionReader(Options options)
        {
            _options = options;
            _animatableColorParser = new AnimatableColorParser(!options.HasFlag(Options.DoNotIgnoreAlpha));
        }

        static LottieComposition ReadLottieCompositionFromJson(
            ref Reader jsonReader,
            Options options,
            out IReadOnlyList<(string Code, string Description)> issues)
        {
            var reader = new LottieCompositionReader(options);
            LottieComposition result = null;
            try
            {
                result = reader.ParseLottieComposition(ref jsonReader);
            }
            catch (JsonReaderException e)
            {
                reader._issues.FatalError(e.Message);
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
            string version = null;
            double? framesPerSecond = null;
            double? inPoint = null;
            double? outPoint = null;
            double? width = null;
            double? height = null;
            string name = null;
            bool? is3d = null;
            var assets = Array.Empty<Asset>();
            var chars = Array.Empty<Char>();
            var fonts = Array.Empty<Font>();
            var layers = Array.Empty<Layer>();
            var markers = Array.Empty<Marker>();
            Dictionary<string, GenericDataObject> extraData = null;

            ConsumeToken(ref reader);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.Comment:
                        // Ignore comments.
                        ConsumeToken(ref reader);
                        break;

                    case JsonTokenType.PropertyName:
                        var currentProperty = reader.GetString();

                        ConsumeToken(ref reader);

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
                                _issues.UnexpectedField(currentProperty);
                                if (extraData is null)
                                {
                                    extraData = new Dictionary<string, GenericDataObject>();
                                }

                                extraData.Add(currentProperty, JsonToGenericData.JTokenToGenericData(JToken.Load(reader.NewtonsoftReader, s_jsonLoadSettings)));
                                break;
                        }

                        break;

                    case JsonTokenType.EndObject:
                        {
                            // Check that the required properties were found. If any are missing, throw.
                            if (version is null)
                            {
                                throw Exception("Version parameter not found.", ref reader);
                            }

                            if (!width.HasValue)
                            {
                                throw Exception("Width parameter not found.", ref reader);
                            }

                            if (!height.HasValue)
                            {
                                throw Exception("Height parameter not found.", ref reader);
                            }

                            if (!inPoint.HasValue)
                            {
                                throw Exception("Start frame parameter not found.", ref reader);
                            }

                            if (!outPoint.HasValue)
                            {
                                Exception("End frame parameter not found.", ref reader);
                            }

                            if (layers is null)
                            {
                                throw Exception("No layers found.", ref reader);
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
                        throw UnexpectedTokenException(ref reader);
                }
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
                return obj.StringOrNullProperty("nm") ?? string.Empty;
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
                return obj.StringOrNullProperty("mn") ?? string.Empty;
            }
        }

        Animatable<Color> ReadColorFromC(in LottieJsonObjectElement obj)
            => ReadAnimatableColor(obj.ObjectOrNullProperty("c"));

        Animatable<Opacity> ReadOpacityFromO(in LottieJsonObjectElement obj)
            => ReadOpacityFromObject(obj.ObjectOrNullProperty("o"));

        Animatable<Opacity> ReadOpacityFromObject(in LottieJsonObjectElement? obj)
        {
            return ReadAnimatableOpacity(obj);
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
                return null;
            }

            var points = pointsData.Value;

            var vertices = points.AsArrayProperty("v");
            var inTangents = points.AsArrayProperty("i");
            var outTangents = points.AsArrayProperty("o");
            var isClosed = points.BoolOrNullProperty("c") ?? false;

            if (vertices is null || inTangents is null || outTangents is null)
            {
                throw new LottieCompositionReaderException($"Unable to process points array or tangents. {points}");
            }

            var beziers = new BezierSegment[isClosed ? vertices.Value.Count : Math.Max(vertices.Value.Count - 1, 0)];

            if (beziers.Length > 0)
            {
                // The vertices for the figure.
                var verticesAsVector2 = ReadArrayOfVector2(vertices.Value);

                // The control points that define the cubic beziers between the vertices.
                var inTangentsAsVector2 = ReadArrayOfVector2(inTangents.Value);
                var outTangentsAsVector2 = ReadArrayOfVector2(outTangents.Value);

                if (verticesAsVector2.Length != inTangentsAsVector2.Length ||
                    verticesAsVector2.Length != outTangentsAsVector2.Length)
                {
                    throw new LottieCompositionReaderException($"Invalid path data. {points}");
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

            return new PathGeometry(beziers);
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

        static Rotation ParseRotation(in LottieJsonElement jsonValue) => Rotation.FromDegrees(jsonValue.AsDouble() ?? 0);

        static Trim ParseTrim(in LottieJsonElement jsonValue) => Trim.FromPercent(jsonValue.AsDouble() ?? 0);

        static Vector3 ParseVector3(in LottieJsonElement jsonValue)
        {
            switch (jsonValue.Kind)
            {
                case JsonValueKind.Object:
                    return jsonValue.AsObject()?.AsVector3() ?? Vector3.Zero;
                case JsonValueKind.Array:
                    return jsonValue.AsArray()?.AsVector3() ?? Vector3.Zero;
                default: return default(Vector3);
            }
        }

        static void ExpectToken(ref Reader reader, JsonTokenType token)
        {
            if (reader.TokenType != token)
            {
                throw UnexpectedTokenException(ref reader);
            }
        }

        delegate T Parser<T>(ref Reader reader);

        T[] ParseArray<T>(ref Reader reader, Parser<T> parser)
        {
            ExpectToken(ref reader, JsonTokenType.StartArray);

            IList<T> list = EmptyList<T>.Singleton;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        var result = parser(ref reader);
                        if (result != null)
                        {
                            if (list == EmptyList<T>.Singleton)
                            {
                                list = new List<T>();
                            }

                            list.Add(result);
                        }

                        break;

                    case JsonTokenType.EndArray:
                        return list.ToArray();

                    default:
                        throw UnexpectedTokenException(ref reader);
                }
            }

            throw EofException;
        }

        // Consumes a token from the stream.
        static void ConsumeToken(ref Reader reader)
        {
            if (!reader.Read())
            {
                throw EofException;
            }
        }

        // We got to the end of the file while still reading. Fatal.
        static LottieCompositionReaderException EofException => Exception("EOF");

        // The JSON is malformed - we found an unexpected token. Fatal.
        static LottieCompositionReaderException UnexpectedTokenException(ref Reader reader) => Exception($"Unexpected token: {reader.TokenType}", ref reader);

        static LottieCompositionReaderException UnexpectedTokenException(JsonValueKind kind) => Exception($"Unexpected token: {kind}");

        static LottieCompositionReaderException Exception(string message, ref Reader reader) => Exception($"{message} @ {reader.NewtonsoftReader.Path}");

        static LottieCompositionReaderException Exception(string message) => new LottieCompositionReaderException(message);
    }
}
