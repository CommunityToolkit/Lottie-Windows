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
        Layer ParseLayer(ref Reader reader) => ReadLayer(JCObject.Load(ref reader, s_jsonLoadSettings));

        // May return null if there was a problem reading the layer.
        Layer ReadLayer(JCObject obj)
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

            Layer.LayerType? layerType = TyToLayerType(obj.GetNamedNumber("ty", double.NaN));

            if (!layerType.HasValue)
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

        void ReadTextData(JCObject obj)
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

        IEnumerable<Mask> ReadMaskProperties(JCArray array)
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
    }
}