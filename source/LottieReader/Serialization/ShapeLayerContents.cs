// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        ShapeLayerContent ReadShapeContent(JCObject obj)
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

        ShapeGroup ReadShapeGroup(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        void ReadShapeLayerContentArgs(JCObject obj, ref ShapeLayerContent.ShapeLayerContentArgs args)
        {
            args.Name = ReadName(obj);
            args.MatchName = ReadMatchName(obj);
            args.BlendMode = BmToBlendMode(obj.GetNamedNumber("bm", 0));
        }

        // "st"
        SolidColorStroke ReadSolidColorStroke(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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
        ShapeLayerContent ReadGradientStroke(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        LinearGradientStroke ReadLinearGradientStroke(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        RadialGradientStroke ReadRadialGradientStroke(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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
        SolidColorFill ReadSolidColorFill(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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
        ShapeLayerContent ReadGradientFill(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        RadialGradientFill ReadRadialGradientFill(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        LinearGradientFill ReadLinearGradientFill(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        Ellipse ReadEllipse(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        Polystar ReadPolystar(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "ix");

            var direction = ReadBool(obj, "d") == true;

            var type = SyToPolystarType(obj.GetNamedNumber("sy", double.NaN));

            if (!type.HasValue)
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
                type.Value,
                points,
                position,
                rotation,
                innerRadius,
                outerRadius,
                innerRoundedness,
                outerRoundedness);
        }

        Rectangle ReadRectangle(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        Path ReadPath(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        TrimPath ReadTrimPath(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        Repeater ReadRepeater(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            var count = ReadAnimatableFloat(obj.GetNamedObject("c"));
            var offset = ReadAnimatableFloat(obj.GetNamedObject("o"));
            var transform = ReadRepeaterTransform(obj.GetNamedObject("tr"), in shapeLayerContentArgs);
            return new Repeater(in shapeLayerContentArgs, count, offset, transform);
        }

        MergePaths ReadMergePaths(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(obj, "hd");

            var mergeMode = MmToMergeMode(obj.GetNamedNumber("mm"));
            AssertAllFieldsRead(obj);
            return new MergePaths(
                in shapeLayerContentArgs,
                mergeMode);
        }

        RoundedCorner ReadRoundedCorner(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        ShapeFill.PathFillType ReadFillType(JCObject obj)
        {
            var isWindingFill = ReadBool(obj, "r") == true;
            return isWindingFill ? ShapeFill.PathFillType.Winding : ShapeFill.PathFillType.EvenOdd;
        }

        // Reads the transform for a repeater. Repeater transforms are the same as regular transforms
        // except they have an extra couple properties.
        RepeaterTransform ReadRepeaterTransform(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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

        Transform ReadTransform(JCObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
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
    }
}