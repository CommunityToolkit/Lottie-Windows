// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// If defined, an issue will be reported for each field that is discovered
// but not parsed. This is used to help test that parsing is complete.
#define CheckForUnparsedFields

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PathGeometry = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Sequence<Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.BezierSegment>;

#if CheckForUnparsedFields
using JArray = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.CheckedJsonArray;
using JObject = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.CheckedJsonObject;
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    // See: https://github.com/airbnb/lottie-web/tree/master/docs/json for the (usually out-of-date) schema.
    // See: https://helpx.adobe.com/pdf/after_effects_reference.pdf for the After Effects semantics.
#if PUBLIC
    public
#endif
    sealed class LottieCompositionReader
    {
        static readonly AnimatableParser<double> s_animatableFloatParser = CreateAnimatableParser(ReadFloat);
        static readonly AnimatableParser<Opacity> s_animatableOpacityParser = CreateAnimatableParser(ReadOpacity);
        static readonly AnimatableParser<Rotation> s_animatableRotationParser = CreateAnimatableParser(ReadRotation);
        static readonly AnimatableParser<Trim> s_animatableTrimParser = CreateAnimatableParser(ReadTrim);
        static readonly AnimatableParser<PathGeometry> s_animatableGeometryParser = new AnimatableGeometryParser();
        static readonly AnimatableParser<Vector3> s_animatableVector3Parser = CreateAnimatableParser(ReadVector3);
        static readonly Animatable<double> s_animatable_0 = new Animatable<double>(0, null);
        static readonly JsonLoadSettings s_jsonLoadSettings = new JsonLoadSettings
        {
            // Ignore commands and line info. Not needed and makes the parser a bit faster.
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Ignore,
        };

        readonly AnimatableColorParser _animatableColorParser;
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
                                    shapes = ReadShapes(JObject.Load(reader, s_jsonLoadSettings));
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

        IEnumerable<Font> ParseFonts(JsonReader reader)
        {
            var fontsObject = JObject.Load(reader, s_jsonLoadSettings);
            foreach (JObject item in fontsObject.GetNamedArray("list"))
            {
                var fName = item.GetNamedString("fName");
                var fFamily = item.GetNamedString("fFamily");
                var fStyle = item.GetNamedString("fStyle");
                var ascent = ReadFloat(item.GetNamedValue("ascent"));
                AssertAllFieldsRead(item);
                yield return new Font(fName, fFamily, fStyle, ascent);
            }

            AssertAllFieldsRead(fontsObject);
        }

        IEnumerable<Layer> ParseLayers(JsonReader reader) =>
            LoadArrayOfJObjects(reader).Select(o => ReadLayer(o)).Where(l => l != null);

        // May return null if there was a problem reading the layer.
        Layer ReadLayer(JObject obj)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "bounds");
            IgnoreFieldThatIsNotYetSupported(obj, "sy");
            IgnoreFieldThatIsNotYetSupported(obj, "td");

            // Field 'hasMask' is deprecated and thus we are intentionally ignoring it
            IgnoreFieldIntentionally(obj, "hasMask");

            var layerArgs = default(Layer.LayerArgs);

            layerArgs.Name = ReadName(obj);
            var index = ReadInt(obj, "ind");

            if (!index.HasValue)
            {
                return null;
            }

            layerArgs.Index = index.Value;
            layerArgs.Parent = ReadInt(obj, "parent");
            layerArgs.Is3d = ReadBool(obj, "ddd") == true;
            layerArgs.AutoOrient = ReadBool(obj, "ao") == true;
            layerArgs.BlendMode = BmToBlendMode(obj.GetNamedNumber("bm", 0));
            layerArgs.IsHidden = ReadBool(obj, "hd") == true;
            var render = ReadBool(obj, "render") != false;

            if (!render)
            {
                _issues.LayerWithRenderFalse();
                return null;
            }

            // Warnings
            if (layerArgs.Name.EndsWith(".ai") || obj.GetNamedString("cl", string.Empty) == "ai")
            {
                _issues.IllustratorLayers();
            }

            if (obj.ContainsKey("ef"))
            {
                _issues.LayerEffectsIsNotSupported(layerArgs.Name);
            }

            // ----------------------
            // Layer Transform
            // ----------------------
            var shapeLayerContentArgs = default(ShapeLayerContent.ShapeLayerContentArgs);
            ReadShapeLayerContentArgs(obj, ref shapeLayerContentArgs);
            layerArgs.Transform = ReadTransform(obj.GetNamedObject("ks"), in shapeLayerContentArgs);

            // ------------------------------
            // Layer Animation
            // ------------------------------
            layerArgs.TimeStretch = obj.GetNamedNumber("sr", 1.0);

            // Time when the layer starts
            layerArgs.StartFrame = obj.GetNamedNumber("st");

            // Time when the layer becomes visible.
            layerArgs.InFrame = obj.GetNamedNumber("ip");
            layerArgs.OutFrame = obj.GetNamedNumber("op");

            // NOTE: The spec specifies this as 'maskProperties' but the BodyMovin tool exports
            // 'masksProperties' with the plural 'masks'.
            var maskProperties = obj.GetNamedArray("masksProperties", null);
            layerArgs.Masks = maskProperties != null ? ReadMaskProperties(maskProperties) : null;

            layerArgs.LayerMatteType = TTToMatteType(obj.GetNamedNumber("tt", (double)Layer.MatteType.None));

            var (isLayerTypeValid, layerType) = TyToLayerType(obj.GetNamedNumber("ty", double.NaN));

            if (!isLayerTypeValid)
            {
                return null;
            }

            switch (layerType)
            {
                case Layer.LayerType.PreComp:
                    {
                        var refId = obj.GetNamedString("refId", string.Empty);
                        var width = obj.GetNamedNumber("w");
                        var height = obj.GetNamedNumber("h");
                        var tm = obj.GetNamedObject("tm", null);
                        if (tm != null)
                        {
                            _issues.TimeRemappingOfPreComps();
                        }

                        AssertAllFieldsRead(obj);
                        return new PreCompLayer(in layerArgs, refId, width, height);
                    }

                case Layer.LayerType.Solid:
                    {
                        var solidWidth = ReadInt(obj, "sw").Value;
                        var solidHeight = ReadInt(obj, "sh").Value;
                        var solidColor = ReadColorFromString(obj.GetNamedString("sc"));

                        AssertAllFieldsRead(obj);
                        return new SolidLayer(in layerArgs, solidWidth, solidHeight, solidColor);
                    }

                case Layer.LayerType.Image:
                    {
                        var refId = obj.GetNamedString("refId", string.Empty);

                        AssertAllFieldsRead(obj);
                        return new ImageLayer(in layerArgs, refId);
                    }

                case Layer.LayerType.Null:
                    AssertAllFieldsRead(obj);
                    return new NullLayer(in layerArgs);

                case Layer.LayerType.Shape:
                    {
                        var shapes = ReadShapes(obj);

                        AssertAllFieldsRead(obj);
                        return new ShapeLayer(in layerArgs, shapes);
                    }

                case Layer.LayerType.Text:
                    {
                        // Text layer references an asset.
                        var refId = obj.GetNamedString("refId", string.Empty);

                        // Text data.
                        ReadTextData(obj.GetNamedObject("t"));

                        AssertAllFieldsRead(obj);
                        return new TextLayer(in layerArgs, refId);
                    }

                default: throw Unreachable;
            }
        }

        void ReadTextData(JObject obj)
        {
            // TODO - read text data

            // Animatable text value
            // "t":text
            // "f":fontName
            // "s":size
            // "j":(int)justification
            // "tr":(int)tracking
            // "lh":lineHeight
            // "ls":baselineShift
            // "fc":fillColor
            // "sc":strokeColor
            // "sw":strokeWidth
            // "of":(bool)strokeOverFill
            IgnoreFieldThatIsNotYetSupported(obj, "d");

            IgnoreFieldThatIsNotYetSupported(obj, "p");
            IgnoreFieldThatIsNotYetSupported(obj, "m");

            // Array of animatable text properties (fc:fill color, sc:stroke color, sw:stroke width, t:tracking (float))
            IgnoreFieldThatIsNotYetSupported(obj, "a");
            AssertAllFieldsRead(obj);
        }

        List<ShapeLayerContent> ReadShapes(JObject obj)
        {
            return ReadShapesList(obj.GetNamedArray("shapes", null));
        }

        List<ShapeLayerContent> ReadShapesList(JArray shapesJson)
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

        IEnumerable<Mask> ReadMaskProperties(JArray array)
        {
            foreach (var elem in array)
            {
                var obj = elem.AsObject();

                // Ignoring field 'x' because it is not in the official spec
                // The x property refers to the mask expansion. In AE you can
                // expand or shrink a mask getting a reduced or expanded version of the same shape.
                IgnoreFieldThatIsNotYetSupported(obj, "x");

                var inverted = obj.GetNamedBoolean("inv");
                var name = ReadName(obj);
                var animatedGeometry = ReadAnimatableGeometry(obj.GetNamedObject("pt"));
                var opacity = ReadOpacityFromO(obj);
                var mode = Mask.MaskMode.None;
                var maskMode = obj.GetNamedString("mode");
                switch (maskMode)
                {
                    case "a":
                        mode = Mask.MaskMode.Add;
                        break;
                    case "d":
                        mode = Mask.MaskMode.Darken;
                        break;
                    case "f":
                        mode = Mask.MaskMode.Difference;
                        break;
                    case "i":
                        mode = Mask.MaskMode.Intersect;
                        break;
                    case "l":
                        mode = Mask.MaskMode.Lighten;
                        break;
                    case "n":
                        mode = Mask.MaskMode.None;
                        break;
                    case "s":
                        mode = Mask.MaskMode.Subtract;
                        break;
                    default:
                        _issues.UnexpectedValueForType("MaskMode", maskMode);
                        continue;
                }

                AssertAllFieldsRead(obj);
                yield return new Mask(
                    inverted,
                    name,
                    animatedGeometry,
                    opacity,
                    mode
                );
            }
        }

        static Color ReadColorFromString(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Color.TransparentBlack;
            }
            else
            {
                var index = 1; // Skip '#'

                // '#AARRGGBB'
                byte a = 255;
                if (hex.Length == 9)
                {
                    a = Convert.ToByte(hex.Substring(index, 2), 16);
                    index += 2;
                }

                var r = Convert.ToByte(hex.Substring(index, 2), 16);
                index += 2;
                var g = Convert.ToByte(hex.Substring(index, 2), 16);
                index += 2;
                var b = Convert.ToByte(hex.Substring(index, 2), 16);

                return Color.FromArgb(
                    a / 255.0,
                    r / 255.0,
                    g / 255.0,
                    b / 255.0);
            }
        }

        string ReadName(JObject obj)
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

        string ReadMatchName(JObject obj)
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

        void ReadShapeLayerContentArgs(JObject obj, ref ShapeLayerContent.ShapeLayerContentArgs args)
        {
            args.Name = ReadName(obj);
            args.MatchName = ReadMatchName(obj);
            args.BlendMode = BmToBlendMode(obj.GetNamedNumber("bm", 0));
        }

        ShapeLayerContent ReadShapeContent(JObject obj)
        {
            var args = default(ShapeLayerContent.ShapeLayerContentArgs);
            ReadShapeLayerContentArgs(obj, ref args);

            var type = obj.GetNamedString("ty");

            switch (type)
            {
                case "gr":
                    return ReadShapeGroup(obj, in args);
                case "st":
                    return ReadSolidColorStroke(obj, in args);
                case "gs":
                    return ReadGradientStroke(obj, in args);
                case "fl":
                    return ReadSolidColorFill(obj, in args);
                case "gf":
                    return ReadGradientFill(obj, in args);
                case "tr":
                    return ReadTransform(obj, in args);
                case "el":
                    return ReadEllipse(obj, in args);
                case "sr":
                    return ReadPolystar(obj, in args);
                case "rc":
                    return ReadRectangle(obj, in args);
                case "sh":
                    return ReadPath(obj, in args);
                case "tm":
                    return ReadTrimPath(obj, in args);
                case "mm":
                    return ReadMergePaths(obj, in args);
                case "rd":
                    return ReadRoundedCorner(obj, in args);
                case "rp":
                    return ReadRepeater(obj, in args);
                default:
                    _issues.UnexpectedValueForType("ShapeContentType", type);
                    return null;
            }
        }

        ShapeGroup ReadShapeGroup(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "cix");
            IgnoreFieldThatIsNotYetSupported(obj, "cl");
            IgnoreFieldThatIsNotYetSupported(obj, "ix");
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var numberOfProperties = ReadInt(obj, "np");
            var items = ReadShapesList(obj.GetNamedArray("it", null));
            AssertAllFieldsRead(obj);
            return new ShapeGroup(in shapeLayerContentArgs, items);
        }

        // "st"
        SolidColorStroke ReadSolidColorStroke(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "fillEnabled");
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var color = ReadColorFromC(obj);
            var opacity = ReadOpacityFromO(obj);
            var strokeWidth = ReadAnimatableFloat(obj.GetNamedObject("w"));
            var capType = LcToLineCapType(obj.GetNamedNumber("lc"));
            var joinType = LjToLineJoinType(obj.GetNamedNumber("lj"));
            var miterLimit = obj.GetNamedNumber("ml", 4); // Default miter limit in After Effects is 4

            // Get dash pattern to be set as StrokeDashArray
            Animatable<double> offset = null;
            var dashPattern = new List<double>();
            var dashesJson = obj.GetNamedArray("d", null);
            if (dashesJson != null)
            {
                for (int i = 0; i < dashesJson.Count; i++)
                {
                    var dashObj = dashesJson[i].AsObject();

                    switch (dashObj.GetNamedString("n"))
                    {
                        case "o":
                            offset = ReadAnimatableFloat(dashObj.GetNamedObject("v"));
                            break;
                        case "d":
                        case "g":
                            dashPattern.Add(ReadAnimatableFloat(dashObj.GetNamedObject("v")).InitialValue);
                            break;
                    }
                }
            }

            AssertAllFieldsRead(obj);
            return new SolidColorStroke(
                in shapeLayerContentArgs,
                offset ?? s_animatable_0,
                dashPattern,
                color,
                opacity,
                strokeWidth,
                capType,
                joinType,
                miterLimit);
        }

        // gs
        ShapeLayerContent ReadGradientStroke(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            switch (TToGradientType(obj.GetNamedNumber("t")))
            {
                case GradientType.Linear:
                    return ReadLinearGradientStroke(obj, in shapeLayerContentArgs);
                case GradientType.Radial:
                    return ReadRadialGradientStroke(obj, in shapeLayerContentArgs);
                default:
                    throw Unreachable;
            }
        }

        LinearGradientStroke ReadLinearGradientStroke(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");
            IgnoreFieldThatIsNotYetSupported(obj, "t");
            IgnoreFieldThatIsNotYetSupported(obj, "1");

            var opacity = ReadOpacityFromO(obj);
            var strokeWidth = ReadAnimatableFloat(obj.GetNamedObject("w"));
            var capType = LcToLineCapType(obj.GetNamedNumber("lc"));
            var joinType = LjToLineJoinType(obj.GetNamedNumber("lj"));
            var miterLimit = obj.GetNamedNumber("ml", 4); // Default miter limit in After Effects is 4
            var startPoint = ReadAnimatableVector3(obj.GetNamedObject("s"));
            var endPoint = ReadAnimatableVector3(obj.GetNamedObject("e"));
            ReadAnimatableGradientStops(obj.GetNamedObject("g"), out var gradientStops);

            AssertAllFieldsRead(obj);
            return new LinearGradientStroke(
                in shapeLayerContentArgs,
                opacity,
                strokeWidth,
                capType,
                joinType,
                miterLimit,
                startPoint,
                endPoint,
                gradientStops);
        }

        RadialGradientStroke ReadRadialGradientStroke(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "t");

            IgnoreFieldThatIsNotYetSupported(obj, "h");

            IgnoreFieldThatIsNotYetSupported(obj, "1");

            Animatable<double> highlightLength = null;
            var highlightLengthObject = obj.GetNamedObject("h");
            if (highlightLengthObject != null)
            {
                highlightLength = ReadAnimatableFloat(highlightLengthObject);
            }

            Animatable<double> highlightDegrees = null;
            var highlightAngleObject = obj.GetNamedObject("a");
            if (highlightAngleObject != null)
            {
                highlightDegrees = ReadAnimatableFloat(highlightAngleObject);
            }

            var opacity = ReadOpacityFromO(obj);
            var strokeWidth = ReadAnimatableFloat(obj.GetNamedObject("w"));
            var capType = LcToLineCapType(obj.GetNamedNumber("lc"));
            var joinType = LjToLineJoinType(obj.GetNamedNumber("lj"));
            var miterLimit = obj.GetNamedNumber("ml", 4); // Default miter limit in After Effects is 4
            var startPoint = ReadAnimatableVector3(obj.GetNamedObject("s"));
            var endPoint = ReadAnimatableVector3(obj.GetNamedObject("e"));
            ReadAnimatableGradientStops(obj.GetNamedObject("g"), out var gradientStops);

            AssertAllFieldsRead(obj);
            return new RadialGradientStroke(
                in shapeLayerContentArgs,
                opacity: opacity,
                strokeWidth: strokeWidth,
                capType: capType,
                joinType: joinType,
                miterLimit: miterLimit,
                startPoint: startPoint,
                endPoint: endPoint,
                gradientStops: gradientStops,
                highlightLength: highlightLength,
                highlightDegrees: highlightDegrees);
        }

        // "fl"
        SolidColorFill ReadSolidColorFill(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "fillEnabled");
            IgnoreFieldThatIsNotYetSupported(obj, "cl");
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var fillType = ReadFillType(obj);
            var opacity = ReadOpacityFromO(obj);
            var color = ReadColorFromC(obj);

            AssertAllFieldsRead(obj);
            return new SolidColorFill(in shapeLayerContentArgs, fillType, opacity, color);
        }

        // gf
        ShapeLayerContent ReadGradientFill(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            switch (TToGradientType(obj.GetNamedNumber("t")))
            {
                case GradientType.Linear:
                    return ReadLinearGradientFill(obj, in shapeLayerContentArgs);
                case GradientType.Radial:
                    return ReadRadialGradientFill(obj, in shapeLayerContentArgs);
                default:
                    throw Unreachable;
            }
        }

        RadialGradientFill ReadRadialGradientFill(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");
            IgnoreFieldThatIsNotYetSupported(obj, "1");

            var fillType = ReadFillType(obj);
            var opacity = ReadOpacityFromO(obj);
            var startPoint = ReadAnimatableVector3(obj.GetNamedObject("s"));
            var endPoint = ReadAnimatableVector3(obj.GetNamedObject("e"));
            ReadAnimatableGradientStops(obj.GetNamedObject("g"), out var gradientStops);

            Animatable<double> highlightLength = null;
            var highlightLengthObject = obj.GetNamedObject("h");
            if (highlightLengthObject != null)
            {
                highlightLength = ReadAnimatableFloat(highlightLengthObject);
            }

            Animatable<double> highlightDegrees = null;
            var highlightAngleObject = obj.GetNamedObject("a");
            if (highlightAngleObject != null)
            {
                highlightDegrees = ReadAnimatableFloat(highlightAngleObject);
            }

            AssertAllFieldsRead(obj);
            return new RadialGradientFill(
                in shapeLayerContentArgs,
                fillType: fillType,
                opacity: opacity,
                startPoint: startPoint,
                endPoint: endPoint,
                gradientStops: gradientStops,
                highlightLength: null,
                highlightDegrees: null);
        }

        LinearGradientFill ReadLinearGradientFill(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var fillType = ReadFillType(obj);
            var opacity = ReadOpacityFromO(obj);
            var startPoint = ReadAnimatableVector3(obj.GetNamedObject("s"));
            var endPoint = ReadAnimatableVector3(obj.GetNamedObject("e"));
            ReadAnimatableGradientStops(obj.GetNamedObject("g"), out var gradientStops);

            AssertAllFieldsRead(obj);
            return new LinearGradientFill(
                in shapeLayerContentArgs,
                fillType: fillType,
                opacity: opacity,
                startPoint: startPoint,
                endPoint: endPoint,
                gradientStops: gradientStops);
        }

        Ellipse ReadEllipse(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "closed");
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var position = ReadAnimatableVector3(obj.GetNamedObject("p"));
            var diameter = ReadAnimatableVector3(obj.GetNamedObject("s"));
            var direction = ReadBool(obj, "d") == true;
            AssertAllFieldsRead(obj);
            return new Ellipse(in shapeLayerContentArgs, direction, position, diameter);
        }

        Polystar ReadPolystar(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "ix");

            var direction = ReadBool(obj, "d") == true;

            var (isPolystartTypeValid, type) = SyToPolystarType(obj.GetNamedNumber("sy", double.NaN));

            if (!isPolystartTypeValid)
            {
                return null;
            }

            var points = ReadAnimatableFloat(obj.GetNamedObject("pt"));
            if (points.IsAnimated)
            {
                _issues.PolystarAnimation("points");
            }

            var position = ReadAnimatableVector3(obj.GetNamedObject("p"));
            if (position.IsAnimated)
            {
                _issues.PolystarAnimation("position");
            }

            var rotation = ReadAnimatableFloat(obj.GetNamedObject("r"));
            if (rotation.IsAnimated)
            {
                _issues.PolystarAnimation("rotation");
            }

            var outerRadius = ReadAnimatableFloat(obj.GetNamedObject("or"));
            if (outerRadius.IsAnimated)
            {
                _issues.PolystarAnimation("outer radius");
            }

            var outerRoundedness = ReadAnimatableFloat(obj.GetNamedObject("os"));
            if (outerRoundedness.IsAnimated)
            {
                _issues.PolystarAnimation("outer roundedness");
            }

            Animatable<double> innerRadius;
            Animatable<double> innerRoundedness;

            if (type == Polystar.PolyStarType.Star)
            {
                innerRadius = ReadAnimatableFloat(obj.GetNamedObject("ir"));
                if (innerRadius.IsAnimated)
                {
                    _issues.PolystarAnimation("inner radius");
                }

                innerRoundedness = ReadAnimatableFloat(obj.GetNamedObject("is"));
                if (innerRoundedness.IsAnimated)
                {
                    _issues.PolystarAnimation("inner roundedness");
                }
            }
            else
            {
                innerRadius = null;
                innerRoundedness = null;
            }

            AssertAllFieldsRead(obj);
            return new Polystar(
                in shapeLayerContentArgs,
                direction,
                type,
                points,
                position,
                rotation,
                innerRadius,
                outerRadius,
                innerRoundedness,
                outerRoundedness);
        }

        Rectangle ReadRectangle(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var direction = ReadBool(obj, "d") == true;
            var position = ReadAnimatableVector3(obj.GetNamedObject("p"));
            var size = ReadAnimatableVector3(obj.GetNamedObject("s"));
            var cornerRadius = ReadAnimatableFloat(obj.GetNamedObject("r"));

            AssertAllFieldsRead(obj);
            return new Rectangle(in shapeLayerContentArgs, direction, position, size, cornerRadius);
        }

        Path ReadPath(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "ind");
            IgnoreFieldThatIsNotYetSupported(obj, "ix");
            IgnoreFieldThatIsNotYetSupported(obj, "hd");
            IgnoreFieldThatIsNotYetSupported(obj, "cl");
            IgnoreFieldThatIsNotYetSupported(obj, "closed");

            var geometry = ReadAnimatableGeometry(obj.GetNamedObject("ks"));
            var direction = ReadBool(obj, "d") == true;
            AssertAllFieldsRead(obj);
            return new Path(in shapeLayerContentArgs, direction, geometry);
        }

        TrimPath ReadTrimPath(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "ix");
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var startTrim = ReadAnimatableTrim(obj.GetNamedObject("s"));
            var endTrim = ReadAnimatableTrim(obj.GetNamedObject("e"));
            var offset = ReadAnimatableRotation(obj.GetNamedObject("o"));
            var trimType = MToTrimType(obj.GetNamedNumber("m", 1));
            AssertAllFieldsRead(obj);
            return new TrimPath(
                in shapeLayerContentArgs,
                trimType,
                startTrim,
                endTrim,
                offset);
        }

        Repeater ReadRepeater(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            var count = ReadAnimatableFloat(obj.GetNamedObject("c"));
            var offset = ReadAnimatableFloat(obj.GetNamedObject("o"));
            var transform = ReadRepeaterTransform(obj.GetNamedObject("tr"), in shapeLayerContentArgs);
            return new Repeater(in shapeLayerContentArgs, count, offset, transform);
        }

        MergePaths ReadMergePaths(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var mergeMode = MmToMergeMode(obj.GetNamedNumber("mm"));
            AssertAllFieldsRead(obj);
            return new MergePaths(
                in shapeLayerContentArgs,
                mergeMode);
        }

        RoundedCorner ReadRoundedCorner(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");
            IgnoreFieldThatIsNotYetSupported(obj, "ix");

            var radius = ReadAnimatableFloat(obj.GetNamedObject("r"));
            AssertAllFieldsRead(obj);
            return new RoundedCorner(
                in shapeLayerContentArgs,
                radius);
        }

        ShapeFill.PathFillType ReadFillType(JObject obj)
        {
            var isWindingFill = ReadBool(obj, "r") == true;
            return isWindingFill ? ShapeFill.PathFillType.Winding : ShapeFill.PathFillType.EvenOdd;
        }

        Animatable<Color> ReadColorFromC(JObject obj) =>
            ReadAnimatableColor(obj.GetNamedObject("c", null));

        Animatable<Color> ReadAnimatableColor(JObject obj)
        {
            if (obj is null)
            {
                return new Animatable<Color>(Color.Black, null);
            }

            _animatableColorParser.ParseJson(this, obj, out var keyFrames, out var initialValue);
            return CreateAnimatable(initialValue, keyFrames, ReadInt(obj, "ix"));
        }

        // Reads the transform for a repeater. Repeater transforms are the same as regular transforms
        // except they have an extra couple properties.
        RepeaterTransform ReadRepeaterTransform(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            var startOpacity = ReadOpacityFromObject(obj.GetNamedObject("so", null));
            var endOpacity = ReadOpacityFromObject(obj.GetNamedObject("eo", null));
            var transform = ReadTransform(obj, in shapeLayerContentArgs);
            return new RepeaterTransform(
                in shapeLayerContentArgs,
                transform.Anchor,
                transform.Position,
                transform.ScalePercent,
                transform.Rotation,
                transform.Opacity,
                startOpacity,
                endOpacity);
        }

        Transform ReadTransform(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            var anchorJson = obj.GetNamedObject("a", null);

            var anchor =
                anchorJson != null
                ? ReadAnimatableVector3(anchorJson)
                : new AnimatableVector3(Vector3.Zero, null);

            var positionJson = obj.GetNamedObject("p", null);

            var position =
                positionJson != null
                    ? ReadAnimatableVector3(positionJson)
                    : new AnimatableVector3(Vector3.Zero, null);

            var scaleJson = obj.GetNamedObject("s", null);

            var scalePercent =
                scaleJson != null
                    ? ReadAnimatableVector3(scaleJson)
                    : new AnimatableVector3(new Vector3(100, 100, 100), null);

            var rotationJson = obj.GetNamedObject("r", null) ?? obj.GetNamedObject("rz", null);

            var rotation =
                    rotationJson != null
                        ? ReadAnimatableRotation(rotationJson)
                        : new Animatable<Rotation>(Rotation.None, null);

            var opacity = ReadOpacityFromO(obj);

            return new Transform(in shapeLayerContentArgs, anchor, position, scalePercent, rotation, opacity);
        }

        static bool? ReadBool(JObject obj, string name)
        {
            if (!obj.ContainsKey(name))
            {
                return null;
            }

            var value = obj.GetNamedValue(name);

            switch (value.Type)
            {
                case JTokenType.Boolean:
                    return obj.GetNamedBoolean(name);
                case JTokenType.Integer:
                case JTokenType.Float:
                    return ReadInt(obj, name)?.Equals(1);
                case JTokenType.Null:
                    // Treat a missing value as false.
                    return false;
                case JTokenType.String:
                case JTokenType.Array:
                case JTokenType.Object:
                default:
                    throw UnexpectedTokenException(value.Type);
            }
        }

        static int? ReadInt(JObject obj, string name)
        {
            var value = obj.GetNamedNumber(name, double.NaN);
            if (double.IsNaN(value))
            {
                return null;
            }

            // Newtonsoft has its own casting logic so to bypass this, we first cast to a double and then round
            // because the desired behavior is to round doubles to the nearest value.
            var intValue = unchecked((int)Math.Round((double)value));
            if (value != intValue)
            {
                return null;
            }

            return intValue;
        }

        IAnimatableVector3 ReadAnimatableVector3(JObject obj)
        {
            IgnoreFieldThatIsNotYetSupported(obj, "s");

            // Expressions not supported.
            IgnoreFieldThatIsNotYetSupported(obj, "x");

            var propertyIndex = ReadInt(obj, "ix");
            if (obj.ContainsKey("k"))
            {
                s_animatableVector3Parser.ParseJson(this, obj, out var keyFrames, out var initialValue);
                AssertAllFieldsRead(obj);

                return keyFrames.Any()
                    ? new AnimatableVector3(keyFrames, propertyIndex)
                    : new AnimatableVector3(initialValue, propertyIndex);
            }
            else
            {
                // Split X and Y dimensions
                var x = ReadAnimatableFloat(obj.GetNamedObject("x"));
                var y = ReadAnimatableFloat(obj.GetNamedObject("y"));
                AssertAllFieldsRead(obj);

                return new AnimatableXYZ(x, y, s_animatable_0);
            }
        }

        Animatable<PathGeometry> ReadAnimatableGeometry(JObject obj)
        {
            s_animatableGeometryParser.ParseJson(this, obj, out var keyFrames, out var initialValue);
            var propertyIndex = ReadInt(obj, "ix");

            return keyFrames.Any()
                ? new Animatable<PathGeometry>(keyFrames, propertyIndex)
                : new Animatable<PathGeometry>(initialValue, propertyIndex);
        }

        void ReadAnimatableGradientStops(
            JObject obj,
            out Animatable<Sequence<GradientStop>> gradientStops)
        {
            // Get the number of color stops. This is optional unless there are opacity stops.
            // If this value doesn't exist, all the stops are color stops. If the value exists
            // then this is the number of color stops, and the remaining stops are opacity stops.
            var numberOfColorStops = ReadInt(obj, "p");

            var animatableColorStopsParser = new AnimatableColorStopsParser(numberOfColorStops);
            animatableColorStopsParser.ParseJson(
                this,
                obj.GetNamedObject("k"),
                out var colorKeyFrames,
                out var colorInitialValue);

            var propertyIndex = ReadInt(obj, "ix");

            if (numberOfColorStops.HasValue)
            {
                // There may be opacity stops. Read them.
                var animatableOpacityStopsParser = new AnimatableOpacityStopsParser(numberOfColorStops.Value);
                animatableOpacityStopsParser.ParseJson(
                    this,
                    obj.GetNamedObject("k"),
                    out var opacityKeyFrames,
                    out var opacityInitialValue);

                if (opacityKeyFrames.Any())
                {
                    // There are opacity key frames. The number of color key frames should be the same
                    // (this is asserted in ConcatGradientStopKeyFrames).
                    gradientStops = new Animatable<Sequence<GradientStop>>(
                                            ConcatGradientStopKeyFrames(colorKeyFrames, opacityKeyFrames),
                                            propertyIndex);
                }
                else
                {
                    // There is only an initial opacity value (i.e. no key frames).
                    // There should be no key frames for color either.
                    if (colorKeyFrames.Any())
                    {
                        throw new LottieCompositionReaderException(
                            "Numbers of key frames in opacity gradient stops and color gradient stops are unequal.");
                    }

                    gradientStops = new Animatable<Sequence<GradientStop>>(
                                            new Sequence<GradientStop>(colorInitialValue.Concat(opacityInitialValue)),
                                            propertyIndex);
                }
            }
            else
            {
                // There are only color stops.
                gradientStops = colorKeyFrames.Any()
                    ? new Animatable<Sequence<GradientStop>>(colorKeyFrames, propertyIndex)
                    : new Animatable<Sequence<GradientStop>>(colorInitialValue, propertyIndex);
            }
        }

        static IEnumerable<KeyFrame<Sequence<GradientStop>>> ConcatGradientStopKeyFrames(
            IEnumerable<KeyFrame<Sequence<GradientStop>>> a,
            IEnumerable<KeyFrame<Sequence<GradientStop>>> b)
        {
            var aArray = a.ToArray();
            var bArray = b.ToArray();
            if (aArray.Length != bArray.Length)
            {
                throw new LottieCompositionReaderException(
                    "Numbers of key frames in opacity gradient stops and color gradient stops are unequal.");
            }

            for (var i = 0; i < aArray.Length; i++)
            {
                var aKeyFrame = aArray[i];
                var bKeyFrame = bArray[i];

                if (aKeyFrame.Frame != bKeyFrame.Frame ||
                    aKeyFrame.SpatialControlPoint1 != bKeyFrame.SpatialControlPoint1 ||
                    aKeyFrame.SpatialControlPoint2 != bKeyFrame.SpatialControlPoint2 ||
                    aKeyFrame.Easing != bKeyFrame.Easing)
                {
                    throw new LottieCompositionReaderException(
                        "Opacity gradient stop key frame does not match color gradient stop key frame.");
                }

                yield return new KeyFrame<Sequence<GradientStop>>(
                    aKeyFrame.Frame,
                    new Sequence<GradientStop>(aKeyFrame.Value.Concat(bKeyFrame.Value)),
                    aKeyFrame.SpatialControlPoint1,
                    aKeyFrame.SpatialControlPoint2,
                    aKeyFrame.Easing);
            }
        }

        Animatable<T> ReadAnimatable<T>(AnimatableParser<T> parser, JObject obj)
            where T : IEquatable<T>
        {
            parser.ParseJson(this, obj, out var keyFrames, out var initialValue);
            return CreateAnimatable(initialValue, keyFrames, ReadInt(obj, "ix"));
        }

        Animatable<double> ReadAnimatableFloat(JObject obj) => ReadAnimatable(s_animatableFloatParser, obj);

        Animatable<Opacity> ReadAnimatableOpacity(JObject obj) => ReadAnimatable(s_animatableOpacityParser, obj);

        Animatable<Rotation> ReadAnimatableRotation(JObject obj) => ReadAnimatable(s_animatableRotationParser, obj);

        Animatable<Trim> ReadAnimatableTrim(JObject obj) => ReadAnimatable(s_animatableTrimParser, obj);

        Animatable<Opacity> ReadOpacityFromO(JObject obj)
        {
            var jsonOpacity = obj.GetNamedObject("o", null);
            return ReadOpacityFromObject(jsonOpacity);
        }

        Animatable<Opacity> ReadOpacityFromObject(JObject obj)
        {
            var result = obj != null
                ? ReadAnimatableOpacity(obj)
                : new Animatable<Opacity>(Opacity.Opaque, null);
            return result;
        }

        static Animatable<T> CreateAnimatable<T>(T initialValue, IEnumerable<KeyFrame<T>> keyFrames, int? propertyIndex)
            where T : IEquatable<T>
            => keyFrames.Any()
                ? new Animatable<T>(keyFrames, propertyIndex)
                : new Animatable<T>(initialValue, propertyIndex);

        static Vector3 ReadVector3FromJsonArray(JArray array)
        {
            double x = 0;
            double y = 0;
            double z = 0;
            int i = 0;
            var count = array.Count;
            for (; i < count; i++)
            {
                // NOTE: indexing JsonArray is faster than enumerating it.
                var number = (double)array[i];
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

        static Vector2 ReadVector2FromJsonArray(JArray array)
        {
            double x = 0;
            double y = 0;
            int i = 0;
            var count = array.Count;
            for (; i < count; i++)
            {
                // NOTE: indexing JsonArray is faster than enumerating it.
                var number = (double)array[i];
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

        sealed class AnimatableColorParser : AnimatableParser<Color>
        {
            readonly bool _ignoreAlpha;

            internal AnimatableColorParser(bool ignoreAlpha)
            {
                _ignoreAlpha = ignoreAlpha;
            }

            protected override Color ReadValue(JToken obj)
            {
                var colorArray = obj.AsArray();
                double a = 0;
                double r = 0;
                double g = 0;
                double b = 0;
                int i = 0;
                var count = colorArray.Count;
                for (; i < count; i++)
                {
                    // Note: indexing a JsonArray is faster than enumerating.
                    var jsonValue = colorArray[i];

                    switch (jsonValue.Type)
                    {
                        case JTokenType.Float:
                        case JTokenType.Integer:
                            break;
                        default:
                            if (_ignoreAlpha && i == 3)
                            {
                                // The alpha channel wasn't an expected type, but we are ignoring alpha
                                // so ignore the error.
                                goto AllColorChannelsRead;
                            }

                            throw UnexpectedTokenException(jsonValue.Type);
                    }

                    var number = (double)jsonValue;
                    switch (i)
                    {
                        case 0:
                            r = number;
                            break;
                        case 1:
                            g = number;
                            break;
                        case 2:
                            b = number;
                            break;
                        case 3:
                            a = number;
                            break;
                    }
                }

            AllColorChannelsRead:

                // Treat any missing values as 0.
                // Some versions of Lottie use floats, some use bytes. Assume bytes if any values are > 1.
                if (r > 1 || g > 1 || b > 1 || a > 1)
                {
                    // Convert byte to float.
                    a /= 255;
                    r /= 255;
                    g /= 255;
                    b /= 255;
                }

                return Color.FromArgb(_ignoreAlpha ? 1 : a, r, g, b);
            }
        }

        sealed class AnimatableGeometryParser : AnimatableParser<PathGeometry>
        {
            protected override PathGeometry ReadValue(JToken value)
            {
                JObject pointsData = null;
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

            static Vector2[] ReadVector2Array(JArray array)
            {
                IEnumerable<Vector2> ToVector2Enumerable()
                {
                    var count = array.Count;
                    for (int i = 0; i < count; i++)
                    {
                        yield return ReadVector2FromJsonArray(array[i].AsArray());
                    }
                }

                return ToVector2Enumerable().ToArray();
            }
        }

        sealed class AnimatableColorStopsParser : AnimatableParser<Sequence<GradientStop>>
        {
            // The number of color stops. The opacity stops follow this number
            // of color stops. If not specified, all of the values are color stops.
            readonly int? _colorStopCount;

            internal AnimatableColorStopsParser(int? colorStopCount)
            {
                _colorStopCount = colorStopCount;
            }

            protected override Sequence<GradientStop> ReadValue(JToken obj)
            {
                var gradientStopsData = obj.AsArray().Select(v => (double)v).ToArray();

                // Get the number of color stops. If _colorStopCount wasn't specified, all of
                // the data in the array is for color stops.
                var colorStopsDataLength = _colorStopCount.HasValue
                    ? _colorStopCount.Value * 4
                    : gradientStopsData.Length;

                if (gradientStopsData.Length < colorStopsDataLength)
                {
                    throw new LottieCompositionReaderException("Fewer gradient stop values than expected");
                }

                var colorStopsCount = colorStopsDataLength / 4;
                var colorStops = new ColorGradientStop[colorStopsCount];

                var offset = 0.0;
                var red = 0.0;
                var green = 0.0;
                int i;
                for (i = 0; i < colorStopsDataLength; i++)
                {
                    var value = gradientStopsData[i];
                    switch (i % 4)
                    {
                        case 0:
                            offset = value;
                            break;
                        case 1:
                            red = value;
                            break;
                        case 2:
                            green = value;
                            break;
                        case 3:
                            var blue = value;

                            // Some versions of Lottie use floats, some use bytes. Assume bytes if any values are > 1.
                            if (red > 1 || green > 1 || blue > 1)
                            {
                                // Convert byte to float.
                                red /= 255;
                                green /= 255;
                                blue /= 255;
                            }

                            colorStops[i / 4] = new ColorGradientStop(offset, Color.FromArgb(1, red, green, blue));
                            break;
                    }
                }

                return new Sequence<GradientStop>(colorStops);
            }
        }

        sealed class AnimatableOpacityStopsParser : AnimatableParser<Sequence<GradientStop>>
        {
            // The number of color stops. The opacity stops follow this number of color stops.
            readonly int _colorStopCount;

            internal AnimatableOpacityStopsParser(int colorStopCount)
            {
                _colorStopCount = colorStopCount;
            }

            protected override Sequence<GradientStop> ReadValue(JToken obj)
            {
                var gradientStopsData = obj.AsArray().Skip(_colorStopCount * 4).Select(v => (double)v).ToArray();
                var gradientStops = new OpacityGradientStop[gradientStopsData.Length / 2];
                var offset = 0.0;
                for (int i = 0; i < gradientStopsData.Length; i++)
                {
                    var value = gradientStopsData[i];
                    switch (i % 2)
                    {
                        case 0:
                            offset = value;
                            break;
                        case 1:
                            double opacity = value;

                            // Some versions of Lottie use floats, some use bytes. Assume bytes if any values are > 1.
                            if (opacity > 1)
                            {
                                // Convert byte to float.
                                opacity /= 255;
                            }

                            gradientStops[i / 2] = new OpacityGradientStop(offset, opacity: Opacity.FromFloat(opacity));
                            break;
                    }
                }

                return new Sequence<GradientStop>(gradientStops);
            }
        }

        static SimpleAnimatableParser<T> CreateAnimatableParser<T>(Func<JToken, T> valueReader)
            where T : IEquatable<T>
             => new SimpleAnimatableParser<T>(valueReader);

        // An AnimatableParser that does not need to hold any state, and for which the ReadValue
        // method can be easily expressed as a lambda.
        sealed class SimpleAnimatableParser<T> : AnimatableParser<T>
            where T : IEquatable<T>
        {
            readonly Func<JToken, T> _valueReader;

            internal SimpleAnimatableParser(Func<JToken, T> valueReader)
            {
                _valueReader = valueReader;
            }

            protected override T ReadValue(JToken obj) => _valueReader(obj);
        }

        abstract class AnimatableParser<T>
            where T : IEquatable<T>
        {
            private protected AnimatableParser()
            {
            }

            protected abstract T ReadValue(JToken obj);

            internal void ParseJson(LottieCompositionReader reader, JObject obj, out IEnumerable<KeyFrame<T>> keyFrames, out T initialValue)
            {
                // Deprecated "a" field meant "isAnimated". The existence of key frames means the same thing.
                reader.IgnoreFieldIntentionally(obj, "a");

                keyFrames = Array.Empty<KeyFrame<T>>();
                initialValue = default(T);

                foreach (var field in obj)
                {
                    switch (field.Key)
                    {
                        case "k":
                            {
                                var k = field.Value;
                                if (k.Type == JTokenType.Array)
                                {
                                    var kArray = k.AsArray();
                                    if (HasKeyframes(kArray))
                                    {
                                        keyFrames = ReadKeyFrames(reader, kArray).ToArray();
                                        initialValue = keyFrames.First().Value;
                                    }
                                }

                                if (keyFrames == Array.Empty<KeyFrame<T>>())
                                {
                                    initialValue = ReadValue(k);
                                }
                            }

                            break;

                        // Defines if property is animated. 0 or 1.
                        // Currently ignored because we derive this from the existence of keyframes.
                        case "a":
                            break;

                        // Property index. Used for expressions. Currently ignored because we don't support expressions.
                        case "ix":
                            // Do not report it as an issue - existence of "ix" doesn't mean that an expression is actually used.
                            break;

                        // Extremely rare fields seen in 1 Lottie file. Ignore.
                        case "nm": // Name
                        case "mn": // MatchName
                        case "hd": // IsHidden
                            break;

                        // Property expression. Currently ignored because we don't support expressions.
                        case "x":
                            reader._issues.Expressions();
                            break;
                        default:
                            reader._issues.UnexpectedField(field.Key);
                            break;
                    }
                }
            }

            static bool HasKeyframes(JArray array)
            {
                var firstItem = array[0];
                return firstItem.Type == JTokenType.Object && firstItem.AsObject().ContainsKey("t");
            }

            IEnumerable<KeyFrame<T>> ReadKeyFrames(LottieCompositionReader reader, JArray jsonArray)
            {
                int count = jsonArray.Count;

                if (count == 0)
                {
                    yield break;
                }

                // -
                // Keyframes are encoded in Lottie as an array consisting of a sequence
                // of start value with start frame and easing function. The final entry in the
                // array is the frame at which the last interpolation ends.
                // [
                //   { startValue_1, startFrame_1 },  # interpolates from startValue_1 to startValue_2 from startFrame_1 to startFrame_2
                //   { startValue_2, startFrame_2 },  # interpolates from startValue_2 to startValue_3 from startFrame_2 to startFrame_3
                //   { startValue_3, startFrame_3 },  # interpolates from startValue_3 to startValue_4 from startFrame_3 to startFrame_4
                //   { startValue_4, startFrame_4 }
                // ]
                // Earlier versions of Bodymovin used an endValue in each key frame.
                // [
                //   { startValue_1, endValue_1, startFrame_1 },  # interpolates from startValue_1 to endValue_1 from startFrame_1 to startFrame_2
                //   { startValue_2, endValue_2, startFrame_2 },  # interpolates from startValue_2 to endValue_2 from startFrame_2 to startFrame_3
                //   { startValue_3, endValue_3, startFrame_3 },  # interpolates from startValue_3 to endValue_3 from startFrame_3 to startFrame_4
                //   { startFrame_4 }
                // ]
                //
                // In order to handle the current and old formats, we detect the presence of the endValue field.
                // If there's an endValue field, the keyframes are using the old format.
                //
                // We convert these to keyframes that match the Windows.UI.Composition notion of a keyframe,
                // which is a triple: {endValue, endTime, easingFunction}.
                // An initial keyframe is created to describe the initial value. It has no easing function.
                //
                // -
                T endValue = default(T);

                // The initial keyframe has the same value as the initial value. Easing therefore doesn't
                // matter, but might as well use hold as it's the simplest (it does not interpolate).
                Easing easing = HoldEasing.Instance;

                // SpatialBeziers.
                var ti = default(Vector3);
                var to = default(Vector3);

                // NOTE: indexing an array with GetObjectAt is faster than enumerating.
                for (int i = 0; i < count; i++)
                {
                    var lottieKeyFrame = jsonArray[i].AsObject();

                    // "n" is a name on the keyframe. It is not useful and has been deprecated in Bodymovin.
                    reader.IgnoreFieldIntentionally(lottieKeyFrame, "n");

                    // Read the start frame.
                    var startFrame = lottieKeyFrame.GetNamedNumber("t", 0);

                    if (i == count - 1)
                    {
                        // This is the final key frame.
                        // If parsing the old format, this key frame will just have the "t" startFrame value.
                        // If parsing the new format, this key frame will also have the "s" startValue.
                        var finalStartValue = lottieKeyFrame.GetNamedValue("s");
                        if (finalStartValue is null)
                        {
                            // Old format.
                            yield return new KeyFrame<T>(startFrame, endValue, to, ti, easing);
                        }
                        else
                        {
                            // New format.
                            yield return new KeyFrame<T>(startFrame, ReadValue(finalStartValue), to, ti, easing);
                        }

                        // No more key frames to read.
                        break;
                    }

                    // Read the start value.
                    var startValue = ReadValue(lottieKeyFrame.GetNamedValue("s"));

                    // Output a keyframe that describes how to interpolate to this start value. The easing information
                    // comes from the previous Lottie keyframe.
                    yield return new KeyFrame<T>(startFrame, startValue, to, ti, easing);

                    // Spatial control points.
                    if (lottieKeyFrame.ContainsKey("ti"))
                    {
                        ti = ReadVector3FromJsonArray(lottieKeyFrame.GetNamedArray("ti"));
                        to = ReadVector3FromJsonArray(lottieKeyFrame.GetNamedArray("to"));
                    }

                    // Get the easing to the end value, and get the end value.
                    if (ReadBool(lottieKeyFrame, "h") == true)
                    {
                        // Hold the current value. The next value comes from the start
                        // of the next entry.
                        easing = HoldEasing.Instance;

                        // Synthesize an endValue. This is only used if this is the final frame.
                        endValue = startValue;
                    }
                    else
                    {
                        // Read the easing function parameters. If there are any parameters, it's a CubicBezierEasing.
                        var cp1Json = lottieKeyFrame.GetNamedObject("o", null);
                        var cp2Json = lottieKeyFrame.GetNamedObject("i", null);
                        if (cp1Json != null && cp2Json != null)
                        {
                            var cp1 = new Vector3(ReadFloat(cp1Json.GetNamedValue("x")), ReadFloat(cp1Json.GetNamedValue("y")), 0);
                            var cp2 = new Vector3(ReadFloat(cp2Json.GetNamedValue("x")), ReadFloat(cp2Json.GetNamedValue("y")), 0);
                            easing = new CubicBezierEasing(cp1, cp2);
                        }
                        else
                        {
                            easing = LinearEasing.Instance;
                        }

                        var endValueObject = lottieKeyFrame.GetNamedValue("e");
                        endValue = endValueObject != null ? ReadValue(endValueObject) : default(T);
                    }

                    // "e" is the end value of a key frame but has been deprecated because it should always be equal
                    // to the start value of the next key frame.
                    reader.IgnoreFieldIntentionally(lottieKeyFrame, "e");

                    reader.AssertAllFieldsRead(lottieKeyFrame);
                }
            }
        }

        static double ReadFloat(JToken jsonValue)
        {
            switch (jsonValue.Type)
            {
                case JTokenType.Float:
                case JTokenType.Integer:
                    return (double)jsonValue;
                case JTokenType.Array:
                    {
                        var array = jsonValue.AsArray();
                        switch (array.Count)
                        {
                            case 0:
                                throw UnexpectedTokenException(jsonValue.Type);
                            case 1:
                                return (double)array[0];
                            default:
                                // Some Lottie files have multiple values in arrays that should only have one. Just
                                // take the first value.
                                return (double)array[0];
                        }
                    }

                case JTokenType.Null:
                    // Treat a missing value as 0.
                    return 0.0;

                case JTokenType.Boolean:
                case JTokenType.String:
                case JTokenType.Object:
                default:
                    throw UnexpectedTokenException(jsonValue.Type);
            }
        }

        static Opacity ReadOpacity(JToken jsonValue) => Opacity.FromPercent(ReadFloat(jsonValue));

        static Rotation ReadRotation(JToken jsonValue) => Rotation.FromDegrees(ReadFloat(jsonValue));

        static Trim ReadTrim(JToken jsonValue) => Trim.FromPercent(ReadFloat(jsonValue));

        static Vector2 ReadVector2(JToken jsonValue) => ReadVector2FromJsonArray(jsonValue.AsArray());

        static Vector3 ReadVector3(JToken jsonValue) => ReadVector3FromJsonArray(jsonValue.AsArray());

        BlendMode BmToBlendMode(double bm)
        {
            if (bm == (int)bm)
            {
                switch ((int)bm)
                {
                    case 0: return BlendMode.Normal;
                    case 1: return BlendMode.Multiply;
                    case 2: return BlendMode.Screen;
                    case 3: return BlendMode.Overlay;
                    case 4: return BlendMode.Darken;
                    case 5: return BlendMode.Lighten;
                    case 6: return BlendMode.ColorDodge;
                    case 7: return BlendMode.ColorBurn;
                    case 8: return BlendMode.HardLight;
                    case 9: return BlendMode.SoftLight;
                    case 10: return BlendMode.Difference;
                    case 11: return BlendMode.Exclusion;
                    case 12: return BlendMode.Hue;
                    case 13: return BlendMode.Saturation;
                    case 14: return BlendMode.Color;
                    case 15: return BlendMode.Luminosity;
                }
            }

            _issues.UnexpectedValueForType("BlendMode", bm.ToString());
            return BlendMode.Normal;
        }

        (bool success, Layer.LayerType layerType) TyToLayerType(double ty)
        {
            if (ty == (int)ty)
            {
                switch ((int)ty)
                {
                    case 0: return (true, Layer.LayerType.PreComp);
                    case 1: return (true, Layer.LayerType.Solid);
                    case 2: return (true, Layer.LayerType.Image);
                    case 3: return (true, Layer.LayerType.Null);
                    case 4: return (true, Layer.LayerType.Shape);
                    case 5: return (true, Layer.LayerType.Text);
                }
            }

            _issues.UnexpectedValueForType("LayerType", ty.ToString());
            return (false, Layer.LayerType.Null);
        }

        (bool success, Polystar.PolyStarType type) SyToPolystarType(double sy)
        {
            if (sy == (int)sy)
            {
                switch ((int)sy)
                {
                    case 1: return (true, Polystar.PolyStarType.Star);
                    case 2: return (true, Polystar.PolyStarType.Polygon);
                }
            }

            _issues.UnexpectedValueForType("PolyStartType", sy.ToString());
            return (false, Polystar.PolyStarType.Star);
        }

        ShapeStroke.LineCapType LcToLineCapType(double lc)
        {
            if (lc == (int)lc)
            {
                switch ((int)lc)
                {
                    case 1: return ShapeStroke.LineCapType.Butt;
                    case 2: return ShapeStroke.LineCapType.Round;
                    case 3: return ShapeStroke.LineCapType.Projected;
                }
            }

            _issues.UnexpectedValueForType("LineCapType", lc.ToString());
            return ShapeStroke.LineCapType.Butt;
        }

        ShapeStroke.LineJoinType LjToLineJoinType(double lj)
        {
            if (lj == (int)lj)
            {
                switch ((int)lj)
                {
                    case 1: return ShapeStroke.LineJoinType.Miter;
                    case 2: return ShapeStroke.LineJoinType.Round;
                    case 3: return ShapeStroke.LineJoinType.Bevel;
                }
            }

            _issues.UnexpectedValueForType("LineJoinType", lj.ToString());
            return ShapeStroke.LineJoinType.Miter;
        }

        TrimPath.TrimType MToTrimType(double m)
        {
            if (m == (int)m)
            {
                switch ((int)m)
                {
                    case 1: return TrimPath.TrimType.Simultaneously;
                    case 2: return TrimPath.TrimType.Individually;
                }
            }

            _issues.UnexpectedValueForType("TrimType", m.ToString());
            return TrimPath.TrimType.Simultaneously;
        }

        MergePaths.MergeMode MmToMergeMode(double mm)
        {
            if (mm == (int)mm)
            {
                switch ((int)mm)
                {
                    case 1: return MergePaths.MergeMode.Merge;
                    case 2: return MergePaths.MergeMode.Add;
                    case 3: return MergePaths.MergeMode.Subtract;
                    case 4: return MergePaths.MergeMode.Intersect;
                    case 5: return MergePaths.MergeMode.ExcludeIntersections;
                }
            }

            _issues.UnexpectedValueForType("MergeMode", mm.ToString());
            return MergePaths.MergeMode.Merge;
        }

        GradientType TToGradientType(double t)
        {
            if (t == (int)t)
            {
                switch ((int)t)
                {
                    case 1: return GradientType.Linear;
                    case 2: return GradientType.Radial;
                }
            }

            _issues.UnexpectedValueForType("GradientType", t.ToString());
            return GradientType.Linear;
        }

        enum GradientType
        {
            Linear,
            Radial,
        }

        Layer.MatteType TTToMatteType(double tt)
        {
            if (tt == (int)tt)
            {
                switch ((int)tt)
                {
                    case 0: return Layer.MatteType.None;
                    case 1: return Layer.MatteType.Add;
                    case 2: return Layer.MatteType.Invert;
                }
            }

            _issues.UnexpectedValueForType("MatteType", tt.ToString());
            return Layer.MatteType.None;
        }

        // Indicates that the given field will not be read because we don't yet support it.
        [Conditional("CheckForUnparsedFields")]
        void IgnoreFieldThatIsNotYetSupported(JObject obj, string fieldName)
        {
#if CheckForUnparsedFields
            obj.ReadFields.Add(fieldName);
#endif
        }

        // Indicates that the given field is not read because we don't need to read it.
        [Conditional("CheckForUnparsedFields")]
        void IgnoreFieldIntentionally(JObject obj, string fieldName)
        {
#if CheckForUnparsedFields
            obj.ReadFields.Add(fieldName);
#endif
        }

        // Reports an issue if the given JsonObject has fields that were not read.
        [Conditional("CheckForUnparsedFields")]
        void AssertAllFieldsRead(JObject obj, [CallerMemberName]string memberName = "")
        {
#if CheckForUnparsedFields
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
#endif
        }

        static void ExpectToken(JsonReader reader, JsonToken token)
        {
            if (reader.TokenType != token)
            {
                throw UnexpectedTokenException(reader);
            }
        }

        static bool ParseBool(JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return (long)reader.Value != 0;
                case JsonToken.Float:
                    return (double)reader.Value != 0;
                case JsonToken.Boolean:
                    return (bool)reader.Value;
                default:
                    throw Exception($"Expected a bool, but got {reader.TokenType}", reader);
            }
        }

        static double ParseDouble(JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return (long)reader.Value;
                case JsonToken.Float:
                    return (double)reader.Value;
                case JsonToken.String:
                    if (double.TryParse((string)reader.Value, out var result))
                    {
                        return result;
                    }

                    break;
            }

            throw Exception($"Expected a number, but got {reader.TokenType}", reader);
        }

        static int ParseInt(JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return checked((int)(long)reader.Value);
                case JsonToken.Float:
                    return checked((int)(long)Math.Round((double)reader.Value));
                case JsonToken.String:
                    if (double.TryParse((string)reader.Value, out var result))
                    {
                        return checked((int)(long)Math.Round((double)result));
                    }

                    break;
            }

            throw Exception($"Expected a number, but got {reader.TokenType}", reader);
        }

        // Loads the JObjects in an array.
        static IEnumerable<JObject> LoadArrayOfJObjects(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartArray);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        yield return JObject.Load(reader, s_jsonLoadSettings);
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

        // Consumes an array from the stream.
        void ConsumeArray(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartArray);

            var startArrayCount = 1;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartArray:
                        startArrayCount++;
                        break;
                    case JsonToken.EndArray:
                        startArrayCount--;
                        if (startArrayCount == 0)
                        {
                            return;
                        }

                        break;
                }
            }

            throw EofException;
        }

        // Consumes an object from the stream.
        void ConsumeObject(JsonReader reader)
        {
            ExpectToken(reader, JsonToken.StartObject);

            var objectStartCount = 1;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        objectStartCount++;
                        break;
                    case JsonToken.EndObject:
                        objectStartCount--;
                        if (objectStartCount == 0)
                        {
                            return;
                        }

                        break;
                }
            }

            throw EofException;
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

#if CheckForUnparsedFields
    sealed class CheckedJsonObject : IEnumerable<KeyValuePair<string, JToken>>
    {
        internal Newtonsoft.Json.Linq.JObject Wrapped { get; }

        internal HashSet<string> ReadFields { get; } = new HashSet<string>();

        internal CheckedJsonObject(Newtonsoft.Json.Linq.JObject wrapped)
        {
            Wrapped = wrapped;
        }

        internal static CheckedJsonObject Parse(string input, JsonLoadSettings loadSettings) => new CheckedJsonObject(Newtonsoft.Json.Linq.JObject.Parse(input, loadSettings));

        internal bool ContainsKey(string key)
        {
            ReadFields.Add(key);
            return Wrapped.ContainsKey(key);
        }

        internal bool TryGetValue(string propertyName, out JToken value)
        {
            ReadFields.Add(propertyName);
            return Wrapped.TryGetValue(propertyName, out value);
        }

        internal static CheckedJsonObject Load(JsonReader reader, JsonLoadSettings settings)
        {
            return new CheckedJsonObject(Newtonsoft.Json.Linq.JObject.Load(reader, settings));
        }

        public IEnumerator<KeyValuePair<string, JToken>> GetEnumerator()
        {
            return Wrapped.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Wrapped.GetEnumerator();
        }

        public static implicit operator CheckedJsonObject(Newtonsoft.Json.Linq.JObject value)
        {
            return value is null ? null : new CheckedJsonObject(value);
        }
    }

    sealed class CheckedJsonArray : IList<JToken>
    {
        internal Newtonsoft.Json.Linq.JArray Wrapped { get; }

        internal CheckedJsonArray(Newtonsoft.Json.Linq.JArray wrapped)
        {
            Wrapped = wrapped;
        }

        internal static CheckedJsonArray Load(JsonReader reader, JsonLoadSettings settings)
        {
            return new CheckedJsonArray(Newtonsoft.Json.Linq.JArray.Load(reader, settings));
        }

        public JToken this[int index] { get => Wrapped[index]; set => throw new NotImplementedException(); }

        public int Count => Wrapped.Count;

        public bool IsReadOnly => throw new NotImplementedException();

        public void Add(JToken item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(JToken item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(JToken[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<JToken> GetEnumerator()
        {
            foreach (var value in Wrapped)
            {
                yield return value;
            }
        }

        public int IndexOf(JToken item) => throw new NotImplementedException();

        public void Insert(int index, JToken item) => throw new NotImplementedException();

        public bool Remove(JToken item) => throw new NotImplementedException();

        public void RemoveAt(int index) => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static implicit operator CheckedJsonArray(Newtonsoft.Json.Linq.JArray value)
        {
            return value is null ? null : new CheckedJsonArray(value);
        }
    }

    static class JObjectExtensions
    {
        internal static JToken GetNamedValue(this JObject jObject, string name, JToken defaultValue = null)
        {
            return jObject.TryGetValue(name, out JToken value) ? value : defaultValue;
        }

        internal static string GetNamedString(this JObject jObject, string name, string defaultValue = "")
        {
            return jObject.TryGetValue(name, out JToken value) ? (string)value : defaultValue;
        }

        internal static double GetNamedNumber(this JObject jObject, string name, double defaultValue = double.NaN)
        {
            return jObject.TryGetValue(name, out JToken value) ? (double)value : defaultValue;
        }

        internal static JArray GetNamedArray(this JObject jObject, string name, JArray defaultValue = null)
        {
            return jObject.TryGetValue(name, out JToken value) ? value.AsArray() : defaultValue;
        }

        internal static JObject GetNamedObject(this JObject jObject, string name, JObject defaultValue = null)
        {
            return jObject.TryGetValue(name, out JToken value) ? value.AsObject() : defaultValue;
        }

        internal static bool GetNamedBoolean(this JObject jObject, string name, bool defaultValue = false)
        {
            return jObject.TryGetValue(name, out JToken value) ? (bool)value : defaultValue;
        }
    }

    static class JTokenExtensions
    {
        internal static JObject AsObject(this JToken token)
        {
            try
            {
                return (JObject)token;
            }
            catch (InvalidCastException ex)
            {
                var exceptionString = ex.Message;
                if (!string.IsNullOrWhiteSpace(token.Path))
                {
                    exceptionString += $" Failed to cast to correct type for token in path: {token.Path}.";
                }

                throw new LottieCompositionReaderException(exceptionString, ex);
            }
        }

        internal static JArray AsArray(this JToken token)
        {
            try
            {
                return (JArray)token;
            }
            catch (InvalidCastException ex)
            {
                var exceptionString = ex.Message;
                if (!string.IsNullOrWhiteSpace(token.Path))
                {
                    exceptionString += $" Failed to cast to correct type for token in path: {token.Path}.";
                }

                throw new LottieCompositionReaderException(exceptionString, ex);
            }
        }
    }
#endif
}
