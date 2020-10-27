// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        // May return null if there was a problem reading the layer.
        Layer? ParseLayer(ref Reader reader)
        {
            using var subDocument = reader.ParseElement();

            var obj = subDocument.RootElement.AsObject();

            return obj.HasValue
                ? ReadLayer(obj.Value)
                : null;
        }

        // May return null if there was a problem reading the layer.
        Layer? ReadLayer(in LottieJsonObjectElement obj)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("bounds", "sy", "td");

            // Property 'hasMask' is deprecated and thus we are intentionally ignoring it.
            obj.IgnorePropertyIntentionally("hasMask");

            var layerArgs = default(Layer.LayerArgs);

            layerArgs.Name = ReadName(obj);
            var index = obj.Int32PropertyOrNull("ind");

            if (!index.HasValue)
            {
                return null;
            }

            layerArgs.Index = index.Value;
            layerArgs.Parent = obj.Int32PropertyOrNull("parent");
            layerArgs.Is3d = obj.BoolPropertyOrNull("ddd") == true;
            layerArgs.AutoOrient = obj.BoolPropertyOrNull("ao") == true;
            layerArgs.BlendMode = BmToBlendMode(obj.DoublePropertyOrNull("bm"));
            layerArgs.IsHidden = obj.BoolPropertyOrNull("hd") == true;
            var render = obj.BoolPropertyOrNull("render") != false;

            if (!render)
            {
                _issues.LayerWithRenderFalse();
                return null;
            }

            // Warnings
            if (layerArgs.Name.EndsWith(".ai") || obj.StringPropertyOrNull("cl") == "ai")
            {
                _issues.IllustratorLayers();
            }

            layerArgs.Effects = ReadEffectsList(obj.ArrayPropertyOrNull("ef"), layerArgs.Name);

            // ----------------------
            // Layer Transform
            // ----------------------
            var shapeLayerContentArgs = default(ShapeLayerContent.ShapeLayerContentArgs);
            ReadShapeLayerContentArgs(obj, ref shapeLayerContentArgs);
            layerArgs.Transform = ReadTransform(obj.ObjectPropertyOrNull("ks"), in shapeLayerContentArgs);

            // ------------------------------
            // Layer Animation
            // ------------------------------
            layerArgs.TimeStretch = obj.DoublePropertyOrNull("sr") ?? 1;

            // Time when the layer starts
            layerArgs.StartFrame = obj.DoublePropertyOrNull("st") ?? double.NaN;

            // Time when the layer becomes visible.
            layerArgs.InFrame = obj.DoublePropertyOrNull("ip") ?? double.NaN;
            layerArgs.OutFrame = obj.DoublePropertyOrNull("op") ?? double.NaN;

            // NOTE: The spec specifies this as 'maskProperties' but the BodyMovin tool exports
            // 'masksProperties' with the plural 'masks'.
            var maskProperties = obj.ArrayPropertyOrNull("masksProperties");
            layerArgs.Masks = maskProperties != null
                                    ? ReadMaskProperties(maskProperties.Value).ToArray()
                                    : Array.Empty<Mask>();

            layerArgs.LayerMatteType = TTToMatteType(obj.DoublePropertyOrNull("tt"));

            Layer.LayerType? layerType = TyToLayerType(obj.DoublePropertyOrNull("ty"));

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
                            var refId = obj.StringPropertyOrNull("refId") ?? string.Empty;
                            var width = obj.DoublePropertyOrNull("w") ?? double.NaN;
                            var height = obj.DoublePropertyOrNull("h") ?? double.NaN;
                            var tm = obj.ObjectPropertyOrNull("tm");
                            if (tm != null)
                            {
                                _issues.TimeRemappingOfPreComps();
                            }

                            return new PreCompLayer(in layerArgs, refId, width, height);
                        }

                    case Layer.LayerType.Solid:
                        {
                            var solidWidth = obj.Int32PropertyOrNull("sw") ?? 0;
                            var solidHeight = obj.Int32PropertyOrNull("sh") ?? 0;
                            var solidColor = ReadColorFromString(obj.StringPropertyOrNull("sc") ?? string.Empty);
                            return new SolidLayer(in layerArgs, solidWidth, solidHeight, solidColor);
                        }

                    case Layer.LayerType.Image:
                        {
                            var refId = obj.StringPropertyOrNull("refId") ?? string.Empty;
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
                            var refId = obj.StringPropertyOrNull("refId") ?? string.Empty;

                            // Text data.
                            var t = obj.ObjectPropertyOrNull("t");
                            if (t is null)
                            {
                                return null;
                            }
                            else
                            {
                                ReadTextData(t.Value);
                                return new TextLayer(in layerArgs, refId);
                            }
                        }

                    default: throw Unreachable;
                }
            }
            finally
            {
                obj.AssertAllPropertiesRead();
            }
        }

        ShapeLayerContent[] ReadShapes(ref Reader reader)
        {
            using var subDocument = reader.ParseElement();
            var obj = subDocument.RootElement.AsObject();

            return obj.HasValue
                ? ReadShapes(obj.Value)
                : Array.Empty<ShapeLayerContent>();
        }

        ShapeLayerContent[] ReadShapes(in LottieJsonObjectElement obj)
        {
            return ReadShapesList(obj.ArrayPropertyOrNull("shapes"));
        }

        ShapeLayerContent[] ReadShapesList(in LottieJsonArrayElement? shapesJson)
        {
            ArrayBuilder<ShapeLayerContent> result = default;
            if (shapesJson != null)
            {
                var shapesJsonCount = shapesJson.Value.Count;
                if (shapesJsonCount > 0)
                {
                    result.SetCapacity(shapesJsonCount);
                    for (var i = 0; i < shapesJsonCount; i++)
                    {
                        var shapeObject = shapesJson.Value[i].AsObject();
                        if (shapeObject != null)
                        {
                            result.AddItemIfNotNull(ReadShapeContent(shapeObject.Value));
                        }
                    }
                }
            }

            return result.ToArray();
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

            // Array of animatable text properties (fc:fill color, sc:stroke color, sw:stroke width, t:tracking (float)).
            obj.IgnorePropertyThatIsNotYetSupported("a");
            obj.AssertAllPropertiesRead();
        }

        IEnumerable<Mask> ReadMaskProperties(LottieJsonArrayElement array)
        {
            foreach (var elem in array)
            {
                var objObject = elem.AsObject();
                if (objObject is null)
                {
                    continue;
                }

                var obj = objObject.Value;

                // Ignoring property 'x' because it is not in the official spec.
                // The x property refers to the mask expansion. In AE you can
                // expand or shrink a mask getting a reduced or expanded version of the same shape.
                obj.IgnorePropertyThatIsNotYetSupported("x");

                var inverted = obj.BoolPropertyOrNull("inv") ?? false;
                var name = ReadName(obj);
                var animatedGeometry = ReadAnimatableGeometry(obj.ObjectPropertyOrNull("pt"));
                var opacity = ReadAnimatableOpacity(obj.ObjectPropertyOrNull("o"));
                var maskMode = obj.StringPropertyOrNull("mode") ?? string.Empty;

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
                var index = 1; // Skip '#'.

                // '#AARRGGBB'.
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