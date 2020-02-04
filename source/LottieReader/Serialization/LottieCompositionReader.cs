// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        static readonly Animatable<double> s_animatable_0 = new Animatable<double>(0, null);

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

            return ReadLottieCompositionFromJson(jsonReader, options, out issues);
        }

        LottieCompositionReader(Options options)
        {
            _options = options;
            _animatableColorParser = new AnimatableColorParser(!options.HasFlag(Options.DoNotIgnoreAlpha));
        }

        static LottieComposition ReadLottieCompositionFromJson(
            JsonReader jsonReader,
            Options options,
            out IReadOnlyList<(string Code, string Description)> issues)
        {
            var reader = new LottieCompositionReader(options);
            LottieComposition result = null;
            try
            {
                result = reader.ParseLottieComposition(jsonReader);
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

        LottieComposition ParseLottieComposition(JsonReader reader)
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

            ConsumeToken(reader);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                    case JsonToken.StartArray:
                    case JsonToken.StartConstructor:
                    case JsonToken.Raw:
                    case JsonToken.Integer:
                    case JsonToken.Float:
                    case JsonToken.String:
                    case JsonToken.Boolean:
                    case JsonToken.Null:
                    case JsonToken.Undefined:
                    case JsonToken.EndArray:
                    case JsonToken.EndConstructor:
                    case JsonToken.Date:
                    case JsonToken.Bytes:
                        // Here means the JSON was invalid or our parser got confused. There is no way to
                        // recover from this, so throw.
                        throw UnexpectedTokenException(reader);

                    case JsonToken.Comment:
                        // Ignore comments.
                        ConsumeToken(reader);
                        break;

                    case JsonToken.PropertyName:
                        var currentProperty = (string)reader.Value;

                        ConsumeToken(reader);

                        switch (currentProperty)
                        {
                            case "assets":
                                assets = ParseArrayOf(reader, ParseAsset).ToArray();
                                break;
                            case "chars":
                                chars = ParseArrayOf(reader, ParseChar).ToArray();
                                break;
                            case "ddd":
                                is3d = ParseBool(reader);
                                break;
                            case "fr":
                                framesPerSecond = ParseDouble(reader);
                                break;
                            case "fonts":
                                fonts = ParseFonts(reader).ToArray();
                                break;
                            case "layers":
                                layers = ParseLayers(reader).ToArray();
                                break;
                            case "h":
                                height = ParseDouble(reader);
                                break;
                            case "ip":
                                inPoint = ParseDouble(reader);
                                break;
                            case "op":
                                outPoint = ParseDouble(reader);
                                break;
                            case "markers":
                                markers = ParseArrayOf(reader, ParseMarker).ToArray();
                                break;
                            case "nm":
                                name = (string)reader.Value;
                                break;
                            case "v":
                                version = (string)reader.Value;
                                break;
                            case "w":
                                width = ParseDouble(reader);
                                break;

                            // Treat any other property as an extension of the BodyMovin format.
                            default:
                                _issues.UnexpectedField(currentProperty);
                                if (extraData is null)
                                {
                                    extraData = new Dictionary<string, GenericDataObject>();
                                }

                                extraData.Add(currentProperty, JsonToGenericData.JTokenToGenericData(JToken.Load(reader, s_jsonLoadSettings)));
                                break;
                        }

                        break;

                    case JsonToken.EndObject:
                        {
                            // Check that the required fields were found. If any are missing, throw.
                            if (version is null)
                            {
                                throw Exception("Version parameter not found.", reader);
                            }

                            if (!width.HasValue)
                            {
                                throw Exception("Width parameter not found.", reader);
                            }

                            if (!height.HasValue)
                            {
                                throw Exception("Height parameter not found.", reader);
                            }

                            if (!inPoint.HasValue)
                            {
                                throw Exception("Start frame parameter not found.", reader);
                            }

                            if (!outPoint.HasValue)
                            {
                                Exception("End frame parameter not found.", reader);
                            }

                            if (layers is null)
                            {
                                throw Exception("No layers found.", reader);
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

                    default:
                        throw UnexpectedTokenException(reader);
                }
            }

            throw EofException;
        }

        Marker ParseMarker(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartObject);

            string name = null;
            double durationMilliseconds = 0;
            double frame = 0;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        var currentProperty = (string)reader.Value;

                        ConsumeToken(reader);

                        switch (currentProperty)
                        {
                            case "cm":
                                name = (string)reader.Value;
                                break;
                            case "dr":
                                durationMilliseconds = ParseDouble(reader);
                                break;
                            case "tm":
                                frame = ParseDouble(reader);
                                break;
                            default:
                                _issues.IgnoredField(currentProperty);
                                reader.Skip();
                                break;
                        }

                        break;
                    case JsonToken.EndObject:
                        return new Marker(name: name, frame: frame, durationMilliseconds: durationMilliseconds);
                    default:
                        throw UnexpectedTokenException(reader);
                }
            }

            throw EofException;
        }

        IEnumerable<Layer> ParseLayers(JsonReader reader) =>
            LoadArrayOfJCObjects(reader).Select(o => ReadLayer(o)).Where(l => l != null);

        List<ShapeLayerContent> ReadShapes(JCObject obj)
        {
            return ReadShapesList(obj.GetNamedArray("shapes", null));
        }

        List<ShapeLayerContent> ReadShapesList(JCArray shapesJson)
        {
            var shapes = new List<ShapeLayerContent>();
            if (shapesJson != null)
            {
                var shapesJsonCount = shapesJson.Count;
                shapes.Capacity = shapesJsonCount;
                for (var i = 0; i < shapesJsonCount; i++)
                {
                    var item = ReadShapeContent(shapesJson[i].AsObject());
                    if (item != null)
                    {
                        shapes.Add(item);
                    }
                }
            }

            return shapes;
        }

        string ReadName(JCObject obj)
        {
            if (_options.HasFlag(Options.IgnoreNames))
            {
                IgnoreFieldIntentionally(obj, "nm");
                return string.Empty;
            }
            else
            {
                return obj.GetNamedString("nm", string.Empty);
            }
        }

        string ReadMatchName(JCObject obj)
        {
            if (_options.HasFlag(Options.IgnoreMatchNames))
            {
                IgnoreFieldIntentionally(obj, "mn");
                return string.Empty;
            }
            else
            {
                return obj.GetNamedString("mn", string.Empty);
            }
        }

        Animatable<Color> ReadColorFromC(JCObject obj) =>
            ReadAnimatableColor(obj.GetNamedObject("c", null));

        Animatable<Opacity> ReadOpacityFromO(JCObject obj)
        {
            var jsonOpacity = obj.GetNamedObject("o", null);
            return ReadOpacityFromObject(jsonOpacity);
        }

        Animatable<Opacity> ReadOpacityFromObject(JCObject obj)
        {
            var result = obj != null
                ? ReadAnimatableOpacity(obj)
                : new Animatable<Opacity>(Opacity.Opaque, null);
            return result;
        }

        static PathGeometry ReadGeometry(JToken value)
        {
            JCObject pointsData = null;
            if (value.Type == JTokenType.Array)
            {
                var firstItem = value.AsArray().First();
                var firstItemAsObject = firstItem.AsObject();
                if (firstItem.Type == JTokenType.Object && firstItemAsObject.ContainsKey("v"))
                {
                    pointsData = firstItemAsObject;
                }
            }
            else if (value.Type == JTokenType.Object && value.AsObject().ContainsKey("v"))
            {
                pointsData = value.AsObject();
            }

            if (pointsData is null)
            {
                return null;
            }

            var vertices = pointsData.GetNamedArray("v", null);
            var inTangents = pointsData.GetNamedArray("i", null);
            var outTangents = pointsData.GetNamedArray("o", null);
            var isClosed = pointsData.GetNamedBoolean("c", false);

            if (vertices is null || inTangents is null || outTangents is null)
            {
                throw new LottieCompositionReaderException($"Unable to process points array or tangents. {pointsData}");
            }

            var beziers = new BezierSegment[isClosed ? vertices.Count : Math.Max(vertices.Count - 1, 0)];

            if (beziers.Length > 0)
            {
                // The vertices for the figure.
                var verticesAsVector2 = ReadVector2Array(vertices);

                // The control points that define the cubic beziers between the vertices.
                var inTangentsAsVector2 = ReadVector2Array(inTangents);
                var outTangentsAsVector2 = ReadVector2Array(outTangents);

                if (verticesAsVector2.Length != inTangentsAsVector2.Length ||
                    verticesAsVector2.Length != outTangentsAsVector2.Length)
                {
                    throw new LottieCompositionReaderException($"Invalid path data. {pointsData}");
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

        static Opacity ReadOpacity(JToken jsonValue) => Opacity.FromPercent(ReadFloat(jsonValue));

        static Rotation ReadRotation(JToken jsonValue) => Rotation.FromDegrees(ReadFloat(jsonValue));

        static Trim ReadTrim(JToken jsonValue) => Trim.FromPercent(ReadFloat(jsonValue));

        // Indicates that the given field will not be read because we don't yet support it.
        void IgnoreFieldThatIsNotYetSupported(JCObject obj, string fieldName)
        {
            obj.ReadFields.Add(fieldName);
        }

        // Indicates that the given field is not read because we don't need to read it.
        void IgnoreFieldIntentionally(JCObject obj, string fieldName)
        {
            obj.ReadFields.Add(fieldName);
        }

        // Reports an issue if the given JsonObject has fields that were not read.
        void AssertAllFieldsRead(JCObject obj, [CallerMemberName]string memberName = "")
        {
            var read = obj.ReadFields;
            var unread = new List<string>();
            foreach (var pair in obj)
            {
                if (!read.Contains(pair.Key))
                {
                    unread.Add(pair.Key);
                }
            }

            unread.Sort();
            foreach (var unreadField in unread)
            {
                _issues.IgnoredField($"{memberName}.{unreadField}");
            }
        }

        static void ExpectToken(JsonReader reader, JsonToken token)
        {
            if (reader.TokenType != token)
            {
                throw UnexpectedTokenException(reader);
            }
        }

        // Loads the JCObjects in an array.
        static IEnumerable<JCObject> LoadArrayOfJCObjects(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartArray);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        yield return JCObject.Load(reader, s_jsonLoadSettings);
                        break;
                    case JsonToken.EndArray:
                        yield break;
                    default:
                        throw UnexpectedTokenException(reader);
                }
            }

            throw EofException;
        }

        IEnumerable<T> ParseArrayOf<T>(JsonReader reader, Func<JsonReader, T> parser)
        {
            ExpectToken(reader, JsonToken.StartArray);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        var result = parser(reader);
                        if (result != null)
                        {
                            yield return result;
                        }

                        break;

                    case JsonToken.EndArray:
                        yield break;

                    default:
                        throw UnexpectedTokenException(reader);
                }
            }
        }

        // Consumes a token from the stream.
        static void ConsumeToken(JsonReader reader)
        {
            if (!reader.Read())
            {
                throw EofException;
            }
        }

        // We got to the end of the file while still reading. Fatal.
        static LottieCompositionReaderException EofException => Exception("EOF");

        // The JSON is malformed - we found an unexpected token. Fatal.
        static LottieCompositionReaderException UnexpectedTokenException(JsonReader reader) => Exception($"Unexpected token: {reader.TokenType}", reader);

        static LottieCompositionReaderException UnexpectedTokenException(JTokenType tokenType) => Exception($"Unexpected token: {tokenType}");

        static LottieCompositionReaderException Exception(string message, JsonReader reader) => Exception($"{message} @ {reader.Path}");

        static LottieCompositionReaderException Exception(string message) => new LottieCompositionReaderException(message);

        // The code we hit is supposed to be unreachable. This indicates a bug.
        static Exception Unreachable => new InvalidOperationException("Unreachable code executed");
    }
}
