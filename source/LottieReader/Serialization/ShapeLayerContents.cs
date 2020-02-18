﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        ShapeLayerContent ReadShapeContent(in LottieJsonObjectElement obj)
        {
            var args = default(ShapeLayerContent.ShapeLayerContentArgs);
            ReadShapeLayerContentArgs(obj, ref args);

            var type = obj.StringOrNullProperty("ty") ?? string.Empty;

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

        ShapeGroup ReadShapeGroup(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("cix", "cl", "ix", "hd");

            var numberOfProperties = obj.Int32OrNullProperty("np");
            var items = ReadShapesList(obj.AsArrayProperty("it"));
            obj.AssertAllPropertiesRead();
            return new ShapeGroup(in shapeLayerContentArgs, items);
        }

        void ReadShapeLayerContentArgs(
            in LottieJsonObjectElement obj,
            ref ShapeLayerContent.ShapeLayerContentArgs args)
        {
            args.Name = ReadName(obj);
            args.MatchName = ReadMatchName(obj);
            args.BlendMode = BmToBlendMode(obj.DoubleOrNullProperty("bm"));
        }

        // "st"
        SolidColorStroke ReadSolidColorStroke(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("fillEnabled", "hd");

            var color = ReadColorFromC(obj);
            var opacity = ReadOpacityFromO(obj);
            var strokeWidth = ReadAnimatableFloat(obj.ObjectOrNullProperty("w"));
            var capType = LcToLineCapType(obj.DoubleOrNullProperty("lc"));
            var joinType = LjToLineJoinType(obj.DoubleOrNullProperty("lj"));
            var miterLimit = obj.DoubleOrNullProperty("ml") ?? 4; // Default miter limit in After Effects is 4

            // Get dash pattern to be set as StrokeDashArray
            Animatable<double> offset = null;
            var dashPattern = new List<double>();
            var dashes = obj.AsArrayProperty("d");
            if (dashes != null)
            {
                var dashesArray = dashes.Value;

                for (int i = 0; i < dashesArray.Count; i++)
                {
                    var dashObj = dashesArray[i].AsObject();

                    switch (dashObj?.StringOrNullProperty("n"))
                    {
                        case "o":
                            offset = ReadAnimatableFloat(dashObj?.ObjectOrNullProperty("v"));
                            break;
                        case "d":
                        case "g":
                            dashPattern.Add(ReadAnimatableFloat(dashObj?.ObjectOrNullProperty("v")).InitialValue);
                            break;
                    }
                }
            }

            obj.AssertAllPropertiesRead();
            return new SolidColorStroke(
                in shapeLayerContentArgs,
                offset ?? s_animatableDoubleZero,
                dashPattern,
                color,
                opacity,
                strokeWidth,
                capType,
                joinType,
                miterLimit);
        }

        // gs
        ShapeLayerContent ReadGradientStroke(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            switch (TToGradientType(obj.DoubleOrNullProperty("t")))
            {
                case GradientType.Linear:
                    return ReadLinearGradientStroke(obj, in shapeLayerContentArgs);
                case GradientType.Radial:
                    return ReadRadialGradientStroke(obj, in shapeLayerContentArgs);
                default:
                    throw Unreachable;
            }
        }

        LinearGradientStroke ReadLinearGradientStroke(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("hd", "t", "1");

            var opacity = ReadOpacityFromO(obj);
            var strokeWidth = ReadAnimatableFloat(obj.ObjectOrNullProperty("w"));
            var capType = LcToLineCapType(obj.DoubleOrNullProperty("lc"));
            var joinType = LjToLineJoinType(obj.DoubleOrNullProperty("lj"));
            var miterLimit = obj.DoubleOrNullProperty("ml") ?? 4; // Default miter limit in After Effects is 4
            var startPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("s"));
            var endPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("e"));
            var gradientStops = ReadAnimatableGradientStops(obj.ObjectOrNullProperty("g"));

            obj.AssertAllPropertiesRead();
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

        RadialGradientStroke ReadRadialGradientStroke(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("t", "h", "1");

            var highlightLengthObject = obj.ObjectOrNullProperty("h");
            var highlightLength = ReadAnimatableFloat(highlightLengthObject);

            var highlightAngleObject = obj.ObjectOrNullProperty("a");
            var highlightDegrees = ReadAnimatableFloat(highlightAngleObject);

            var opacity = ReadOpacityFromO(obj);
            var strokeWidth = ReadAnimatableFloat(obj.ObjectOrNullProperty("w"));
            var capType = LcToLineCapType(obj.DoubleOrNullProperty("lc"));
            var joinType = LjToLineJoinType(obj.DoubleOrNullProperty("lj"));
            var miterLimit = obj.DoubleOrNullProperty("ml") ?? 4; // Default miter limit in After Effects is 4
            var startPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("s"));
            var endPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("e"));
            var gradientStops = ReadAnimatableGradientStops(obj.ObjectOrNullProperty("g"));

            obj.AssertAllPropertiesRead();
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
        SolidColorFill ReadSolidColorFill(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("fillEnabled", "cl", "hd");

            var fillType = ReadFillType(obj);
            var opacity = ReadOpacityFromO(obj);
            var color = ReadColorFromC(obj);

            obj.AssertAllPropertiesRead();
            return new SolidColorFill(in shapeLayerContentArgs, fillType, opacity, color);
        }

        // gf
        ShapeLayerContent ReadGradientFill(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            switch (TToGradientType(obj.DoubleOrNullProperty("t")))
            {
                case GradientType.Linear:
                    return ReadLinearGradientFill(obj, in shapeLayerContentArgs);
                case GradientType.Radial:
                    return ReadRadialGradientFill(obj, in shapeLayerContentArgs);
                default:
                    throw Unreachable;
            }
        }

        RadialGradientFill ReadRadialGradientFill(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("hd", "1");

            var fillType = ReadFillType(obj);
            var opacity = ReadOpacityFromO(obj);
            var startPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("s"));
            var endPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("e"));
            var gradientStops = ReadAnimatableGradientStops(obj.ObjectOrNullProperty("g"));

            var highlightLengthObject = obj.ObjectOrNullProperty("h");
            var highlightLength = ReadAnimatableFloat(highlightLengthObject);

            var highlightAngleObject = obj.ObjectOrNullProperty("a");
            var highlightDegrees = ReadAnimatableFloat(highlightAngleObject);

            obj.AssertAllPropertiesRead();
            return new RadialGradientFill(
                in shapeLayerContentArgs,
                fillType: fillType,
                opacity: opacity,
                startPoint: startPoint,
                endPoint: endPoint,
                gradientStops: gradientStops,
                highlightLength: highlightLength,
                highlightDegrees: highlightDegrees);
        }

        LinearGradientFill ReadLinearGradientFill(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("hd");

            var fillType = ReadFillType(obj);
            var opacity = ReadOpacityFromO(obj);
            var startPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("s"));
            var endPoint = ReadAnimatableVector3(obj.ObjectOrNullProperty("e"));
            var gradientStops = ReadAnimatableGradientStops(obj.ObjectOrNullProperty("g"));

            obj.AssertAllPropertiesRead();
            return new LinearGradientFill(
                in shapeLayerContentArgs,
                fillType: fillType,
                opacity: opacity,
                startPoint: startPoint,
                endPoint: endPoint,
                gradientStops: gradientStops);
        }

        Ellipse ReadEllipse(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("closed", "hd");

            var position = ReadAnimatableVector3(obj.ObjectOrNullProperty("p"));
            var diameter = ReadAnimatableVector3(obj.ObjectOrNullProperty("s"));
            var direction = obj.BoolOrNullProperty("d") == true;
            obj.AssertAllPropertiesRead();
            return new Ellipse(in shapeLayerContentArgs, direction, position, diameter);
        }

        Polystar ReadPolystar(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("ix");

            var direction = obj.BoolOrNullProperty("d") == true;

            var type = SyToPolystarType(obj.DoubleOrNullProperty("sy"));

            if (!type.HasValue)
            {
                return null;
            }

            var polystarType = type.Value;

            var points = ReadAnimatableFloat(obj.ObjectOrNullProperty("pt"));
            if (points.IsAnimated)
            {
                _issues.PolystarAnimation("points");
            }

            var position = ReadAnimatableVector3(obj.ObjectOrNullProperty("p"));
            if (position.IsAnimated)
            {
                _issues.PolystarAnimation("position");
            }

            var rotation = ReadAnimatableFloat(obj.ObjectOrNullProperty("r"));
            if (rotation.IsAnimated)
            {
                _issues.PolystarAnimation("rotation");
            }

            var outerRadius = ReadAnimatableFloat(obj.ObjectOrNullProperty("or"));
            if (outerRadius.IsAnimated)
            {
                _issues.PolystarAnimation("outer radius");
            }

            var outerRoundedness = ReadAnimatableFloat(obj.ObjectOrNullProperty("os"));
            if (outerRoundedness.IsAnimated)
            {
                _issues.PolystarAnimation("outer roundedness");
            }

            Animatable<double> innerRadius;
            Animatable<double> innerRoundedness;

            switch (polystarType)
            {
                case Polystar.PolyStarType.Star:
                    innerRadius = ReadAnimatableFloat(obj.ObjectOrNullProperty("ir"));
                    if (innerRadius.IsAnimated)
                    {
                        _issues.PolystarAnimation("inner radius");
                    }

                    innerRoundedness = ReadAnimatableFloat(obj.ObjectOrNullProperty("is"));
                    if (innerRoundedness.IsAnimated)
                    {
                        _issues.PolystarAnimation("inner roundedness");
                    }

                    break;

                default:
                    innerRadius = null;
                    innerRoundedness = null;
                    break;
            }

            obj.AssertAllPropertiesRead();
            return new Polystar(
                in shapeLayerContentArgs,
                direction,
                polystarType,
                points,
                position,
                rotation,
                innerRadius,
                outerRadius,
                innerRoundedness,
                outerRoundedness);
        }

        Rectangle ReadRectangle(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("hd");

            var direction = obj.BoolOrNullProperty("d") == true;
            var position = ReadAnimatableVector3(obj.ObjectOrNullProperty("p"));
            var size = ReadAnimatableVector3(obj.ObjectOrNullProperty("s"));
            var cornerRadius = ReadAnimatableFloat(obj.ObjectOrNullProperty("r"));

            obj.AssertAllPropertiesRead();
            return new Rectangle(in shapeLayerContentArgs, direction, position, size, cornerRadius);
        }

        Path ReadPath(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("ind", "ix", "hd", "cl", "closed");

            var geometry = ReadAnimatableGeometry(obj.ObjectOrNullProperty("ks"));
            var direction = obj.BoolOrNullProperty("d") == true;
            obj.AssertAllPropertiesRead();
            return new Path(in shapeLayerContentArgs, direction, geometry);
        }

        TrimPath ReadTrimPath(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("ix", "hd");

            var startTrim = ReadAnimatableTrim(obj.ObjectOrNullProperty("s"));
            var endTrim = ReadAnimatableTrim(obj.ObjectOrNullProperty("e"));
            var offset = ReadAnimatableRotation(obj.ObjectOrNullProperty("o"));
            var trimType = MToTrimType(obj.DoubleOrNullProperty("m"));
            obj.AssertAllPropertiesRead();
            return new TrimPath(
                in shapeLayerContentArgs,
                trimType,
                startTrim,
                endTrim,
                offset);
        }

        Repeater ReadRepeater(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            var count = ReadAnimatableFloat(obj.ObjectOrNullProperty("c"));
            var offset = ReadAnimatableFloat(obj.ObjectOrNullProperty("o"));
            var transform = ReadRepeaterTransform(obj.ObjectOrNullProperty("tr"), in shapeLayerContentArgs);
            return new Repeater(in shapeLayerContentArgs, count, offset, transform);
        }

        MergePaths ReadMergePaths(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("hd");

            var mergeMode = MmToMergeMode(obj.DoubleOrNullProperty("mm"));
            obj.AssertAllPropertiesRead();
            return new MergePaths(
                in shapeLayerContentArgs,
                mergeMode);
        }

        RoundedCorner ReadRoundedCorner(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these properties.
            obj.IgnorePropertyThatIsNotYetSupported("hd", "ix");

            var radius = ReadAnimatableFloat(obj.ObjectOrNullProperty("r"));
            obj.AssertAllPropertiesRead();
            return new RoundedCorner(
                in shapeLayerContentArgs,
                radius);
        }

        ShapeFill.PathFillType ReadFillType(in LottieJsonObjectElement obj)
        {
            // If not specified, the fill type is EvenOdd.
            var windingValue = obj.Int32OrNullProperty("r");
            switch (windingValue)
            {
                case 0: return ShapeFill.PathFillType.EvenOdd;
                case 1: return ShapeFill.PathFillType.Winding;

                // TODO - some files have a "2" value. There
                // may be another fill type.
                default: return ShapeFill.PathFillType.EvenOdd;
            }
        }

        // Reads the transform for a repeater. Repeater transforms are the same as regular transforms
        // except they have an extra couple properties.
        RepeaterTransform ReadRepeaterTransform(
            in LottieJsonObjectElement? obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            if (obj is null)
            {
                return new RepeaterTransform(
                    shapeLayerContentArgs,
                    s_animatableVector3Zero,
                    s_animatableVector3Zero,
                    s_animatableVector3Zero,
                    s_animatableRotationNone,
                    s_animatableOpacityOpaque,
                    s_animatableOpacityOpaque,
                    s_animatableOpacityOpaque);
            }
            else
            {
                return ReadRepeaterTransform(obj.Value, shapeLayerContentArgs);
            }
        }

        RepeaterTransform ReadRepeaterTransform(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            obj.IgnorePropertyThatIsNotYetSupported("nm", "ty");

            var startOpacity = ReadOpacityFromObject(obj.ObjectOrNullProperty("so"));
            var endOpacity = ReadOpacityFromObject(obj.ObjectOrNullProperty("eo"));
            var transform = ReadTransform(obj, in shapeLayerContentArgs);

            obj.AssertAllPropertiesRead();
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

        Transform ReadTransform(
            in LottieJsonObjectElement? obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            if (obj is null)
            {
                return new Transform(
                    shapeLayerContentArgs,
                    s_animatableVector3Zero,
                    s_animatableVector3Zero,
                    s_animatableVector3Zero,
                    s_animatableRotationNone,
                    s_animatableOpacityOpaque);
            }
            else
            {
                return ReadTransform(obj.Value, shapeLayerContentArgs);
            }
        }

        Transform ReadTransform(
            in LottieJsonObjectElement obj,
            in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear what these.
            obj.IgnorePropertyThatIsNotYetSupported("sa", "sk");
            obj.IgnorePropertyThatIsNotYetSupported("or", "rx", "ry");
            obj.IgnorePropertyThatIsNotYetSupported("nm", "ty");

            var anchorJson = obj.ObjectOrNullProperty("a");

            var anchor = ReadAnimatableVector3(anchorJson);
            var positionJson = obj.ObjectOrNullProperty("p");
            var position = ReadAnimatableVector3(positionJson);
            var scaleJson = obj.ObjectOrNullProperty("s");

            var scalePercent =
                scaleJson != null
                    ? ReadAnimatableVector3(scaleJson)
                    : new AnimatableVector3(new Vector3(100, 100, 100), null);

            var rotationJson = obj.ObjectOrNullProperty("r") ?? obj.ObjectOrNullProperty("rz");

            var rotation = ReadAnimatableRotation(rotationJson);

            var opacity = ReadOpacityFromO(obj);

            obj.AssertAllPropertiesRead();
            return new Transform(in shapeLayerContentArgs, anchor, position, scalePercent, rotation, opacity);
        }
    }
}