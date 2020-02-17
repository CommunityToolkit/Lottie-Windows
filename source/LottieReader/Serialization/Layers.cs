// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        Layer ParseLayer(ref Reader reader) => ReadLayer(LottieJsonObjectElement.Load(this, ref reader, s_jsonLoadSettings));

        // May return null if there was a problem reading the layer.
        Layer ReadLayer(in LottieJsonObjectElement obj)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("bounds", "sy", "td");

            // Property 'hasMask' is deprecated and thus we are intentionally ignoring it
            obj.IgnorePropertyIntentionally("hasMask");

            var layerArgs = default(Layer.LayerArgs);

            layerArgs.Name = ReadName(obj);
            var index = obj.Int32OrNullProperty("ind");

            if (!index.HasValue)
            {
                return null;
            }

            layerArgs.Index = index.Value;
            layerArgs.Parent = obj.Int32OrNullProperty("parent");
            layerArgs.Is3d = obj.BoolOrNullProperty("ddd") == true;
            layerArgs.AutoOrient = obj.BoolOrNullProperty("ao") == true;
            layerArgs.BlendMode = BmToBlendMode(obj.DoubleOrNullProperty("bm"));
            layerArgs.IsHidden = obj.BoolOrNullProperty("hd") == true;
            var render = obj.BoolOrNullProperty("render") != false;

            if (!render)
            {
                _issues.LayerWithRenderFalse();
                return null;
            }

            // Warnings
            if (layerArgs.Name.EndsWith(".ai") || obj.StringOrNullProperty("cl") == "ai")
            {
                _issues.IllustratorLayers();
            }

            if (obj.ContainsProperty("ef"))
            {
                _issues.LayerEffectsIsNotSupported(layerArgs.Name);
            }

            // ----------------------
            // Layer Transform
            // ----------------------
            var shapeLayerContentArgs = default(ShapeLayerContent.ShapeLayerContentArgs);
            ReadShapeLayerContentArgs(obj, ref shapeLayerContentArgs);
            layerArgs.Transform = ReadTransform(obj.ObjectOrNullProperty("ks").Value, in shapeLayerContentArgs);

            // ------------------------------
            // Layer Animation
            // ------------------------------
            layerArgs.TimeStretch = obj.DoubleOrNullProperty("sr") ?? 1;

            // Time when the layer starts
            layerArgs.StartFrame = obj.DoubleOrNullProperty( "st") ?? double.NaN;

            // Time when the layer becomes visible.
            layerArgs.InFrame = obj.DoubleOrNullProperty("ip") ?? double.NaN;
            layerArgs.OutFrame = obj.DoubleOrNullProperty( "op") ?? double.NaN;

            // NOTE: The spec specifies this as 'maskProperties' but the BodyMovin tool exports
            // 'masksProperties' with the plural 'masks'.
            var maskProperties = obj.ArrayOrNullProperty("masksProperties");
            layerArgs.Masks = maskProperties != null ? ReadMaskProperties(maskProperties.Value) : null;

            layerArgs.LayerMatteType = TTToMatteType(obj.DoubleOrNullProperty( "tt"));

            Layer.LayerType? layerType = TyToLayerType(obj.DoubleOrNullProperty( "ty"));

            if (!layerType.HasValue)
            {
                return null;
            }

            try
            {
                switch (layerType)
                {
                    case Layer.LayerType.PreComp:
                        {
                            var refId = obj.StringOrNullProperty("refId") ?? string.Empty;
                            var width = obj.DoubleOrNullProperty("w") ?? double.NaN;
                            var height = obj.DoubleOrNullProperty("h") ?? double.NaN;
                            var tm = obj.ObjectOrNullProperty("tm");
                            if (tm != null)
                            {
                                _issues.TimeRemappingOfPreComps();
                            }

                            return new PreCompLayer(in layerArgs, refId, width, height);
                        }

                    case Layer.LayerType.Solid:
                        {
                            var solidWidth = obj.Int32OrNullProperty("sw").Value;
                            var solidHeight = obj.Int32OrNullProperty("sh").Value;
                            var solidColor = ReadColorFromString(obj.StringOrNullProperty("sc") ?? string.Empty);
                            return new SolidLayer(in layerArgs, solidWidth, solidHeight, solidColor);
                        }

                    case Layer.LayerType.Image:
                        {
                            var refId = obj.StringOrNullProperty("refId") ?? string.Empty;
                            return new ImageLayer(in layerArgs, refId);
                        }

                    case Layer.LayerType.Null:
                        return new NullLayer(in layerArgs);

                    case Layer.LayerType.Shape:
                        {
                            var shapes = ReadShapes(obj);
                            return new ShapeLayer(in layerArgs, shapes);
                        }

                    case Layer.LayerType.Text:
                        {
                            // Text layer references an asset.
                            var refId = obj.StringOrNullProperty("refId") ?? string.Empty;

                            // Text data.
                            ReadTextData(obj.ObjectOrNullProperty("t").Value);
                            return new TextLayer(in layerArgs, refId);
                        }

                    default: throw Unreachable;
                }
            }
            finally
            {
                obj.AssertAllPropertiesRead();
            }
        }

        List<ShapeLayerContent> ReadShapes(in LottieJsonObjectElement obj)
        {
            return ReadShapesList(obj.ArrayOrNullProperty("shapes"));
        }

        List<ShapeLayerContent> ReadShapesList(in LottieJsonArrayElement? shapesJson)
        {
            var shapes = new List<ShapeLayerContent>();
            if (shapesJson != null)
            {
                var shapesJsonCount = shapesJson.Value.Count;
                shapes.Capacity = shapesJsonCount;
                for (var i = 0; i < shapesJsonCount; i++)
                {
                    var item = ReadShapeContent(shapesJson.Value[i].AsObject().Value);
                    if (item != null)
                    {
                        shapes.Add(item);
                    }
                }
            }

            return shapes;
        }

        void ReadTextData(in LottieJsonObjectElement obj)
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
            obj.IgnorePropertyThatIsNotYetSupported("d", "p", "m");

            // Array of animatable text properties (fc:fill color, sc:stroke color, sw:stroke width, t:tracking (float))
            obj.IgnorePropertyThatIsNotYetSupported("a");
            obj.AssertAllPropertiesRead();
        }

        IEnumerable<Mask> ReadMaskProperties(LottieJsonArrayElement array)
        {
            foreach (var elem in array)
            {
                var obj = elem.AsObject().Value;

                // Ignoring property 'x' because it is not in the official spec
                // The x property refers to the mask expansion. In AE you can
                // expand or shrink a mask getting a reduced or expanded version of the same shape.
                obj.IgnorePropertyThatIsNotYetSupported("x");

                var inverted = obj.BoolOrNullProperty("inv") ?? false;
                var name = ReadName(obj);
                var animatedGeometry = ReadAnimatableGeometry(obj.ObjectOrNullProperty("pt").Value);
                var opacity = ReadOpacityFromO(obj);
                var maskMode = obj.StringOrNullProperty("mode") ?? string.Empty;

                Mask.MaskMode mode;
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

                obj.AssertAllPropertiesRead();
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
    }
}