// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.MetaData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Expressions = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Wui = CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Renders a WinCompData graph as canonical text.
    /// </summary>
    /// <remarks>
    /// Two graphs produce identical text if and only if they are structurally
    /// equivalent, including the sharing of nodes: each node is given an id the first
    /// time it is reached by a depth first walk from the root, and every later reference
    /// to it is written as that id. So a graph in which two parents share one child
    /// cannot produce the same text as one in which they have a child each.
    /// <para/>
    /// This is the oracle for the round trip tests. It is deliberately independent of
    /// both the serializer and the deserializer, and it fails loudly on a node type it
    /// does not know about, so that a type added to WinCompData cannot silently escape
    /// comparison.
    /// </remarks>
    static class CompositionTreeDumper
    {
        /// <summary>
        /// Renders a graph as canonical text.
        /// </summary>
        /// <param name="root">The root of the graph.</param>
        /// <returns>The canonical text of the graph.</returns>
        public static string Dump(Visual root)
        {
            var writer = new Writer();
            writer.WriteVisual(root);
            return writer.ToString();
        }

        sealed class Writer
        {
            readonly StringBuilder _builder = new StringBuilder();

            // Ids are assigned per object, in the order that the objects are first
            // reached. Reference equality is what matters here: two distinct objects with
            // identical contents are different nodes and must get different ids.
            readonly Dictionary<object, int> _ids =
                new Dictionary<object, int>(ReferenceEqualityComparer.Instance);

            int _indent;

            public override string ToString() => _builder.ToString();

            public void WriteVisual(Visual? value)
            {
                if (WriteNodeHeader(value, "Visual"))
                {
                    return;
                }

                WriteCompositionObject(value!);

                WriteOptional("BorderMode", value!.BorderMode);
                WriteOptional("CenterPoint", value.CenterPoint);
                WriteChild("Clip", value.Clip, WriteClip);
                WriteOptional("IsVisible", value.IsVisible);
                WriteOptional("Offset", value.Offset);
                WriteOptional("Opacity", value.Opacity);
                WriteOptional("RotationAngleInDegrees", value.RotationAngleInDegrees);
                WriteOptional("RotationAxis", value.RotationAxis);
                WriteOptional("Scale", value.Scale);
                WriteOptional("Size", value.Size);
                WriteOptional("TransformMatrix", value.TransformMatrix);

                if (value is ContainerVisual container)
                {
                    WriteList("Children", container.Children, WriteVisual);
                }

                switch (value)
                {
                    case ShapeVisual shapeVisual:
                        WriteList("Shapes", shapeVisual.Shapes, WriteShape);
                        WriteChild("ViewBox", shapeVisual.ViewBox, WriteViewBox);
                        break;
                    case SpriteVisual spriteVisual:
                        WriteChild("Brush", spriteVisual.Brush, WriteBrush);
                        WriteChild("Shadow", spriteVisual.Shadow, WriteShadow);
                        break;
                    case LayerVisual layerVisual:
                        WriteChild("Shadow", layerVisual.Shadow, WriteShadow);
                        break;
                    case ContainerVisual _:
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected visual {value.Type}.");
                }

                _indent--;
            }

            public void WriteShape(CompositionShape? value)
            {
                if (WriteNodeHeader(value, "Shape"))
                {
                    return;
                }

                WriteCompositionObject(value!);

                WriteOptional("CenterPoint", value!.CenterPoint);
                WriteOptional("Offset", value.Offset);
                WriteOptional("RotationAngleInDegrees", value.RotationAngleInDegrees);
                WriteOptional("Scale", value.Scale);
                WriteOptional("TransformMatrix", value.TransformMatrix);

                switch (value)
                {
                    case CompositionContainerShape containerShape:
                        WriteList("Shapes", containerShape.Shapes, WriteShape);
                        break;
                    case CompositionSpriteShape spriteShape:
                        WriteChild("FillBrush", spriteShape.FillBrush, WriteBrush);
                        WriteChild("StrokeBrush", spriteShape.StrokeBrush, WriteBrush);
                        WriteChild("Geometry", spriteShape.Geometry, WriteGeometry);
                        WriteOptional("IsStrokeNonScaling", spriteShape.IsStrokeNonScaling);
                        WriteOptional("StrokeDashOffset", spriteShape.StrokeDashOffset);
                        WriteFloats("StrokeDashArray", spriteShape.StrokeDashArray);
                        WriteOptional("StrokeDashCap", spriteShape.StrokeDashCap);
                        WriteOptional("StrokeStartCap", spriteShape.StrokeStartCap);
                        WriteOptional("StrokeEndCap", spriteShape.StrokeEndCap);
                        WriteOptional("StrokeLineJoin", spriteShape.StrokeLineJoin);
                        WriteOptional("StrokeMiterLimit", spriteShape.StrokeMiterLimit);
                        WriteOptional("StrokeThickness", spriteShape.StrokeThickness);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected shape {value.Type}.");
                }

                _indent--;
            }

            public void WriteGeometry(CompositionGeometry? value)
            {
                if (WriteNodeHeader(value, "Geometry"))
                {
                    return;
                }

                WriteCompositionObject(value!);

                WriteOptional("TrimStart", value!.TrimStart);
                WriteOptional("TrimEnd", value.TrimEnd);
                WriteOptional("TrimOffset", value.TrimOffset);

                switch (value)
                {
                    case CompositionPathGeometry pathGeometry:
                        WriteChild(
                            "Path",
                            pathGeometry.Path is null ? null : (CanvasGeometry)pathGeometry.Path.Source,
                            WriteCanvasGeometry);
                        break;
                    case CompositionRoundedRectangleGeometry roundedRectangle:
                        WriteOptional("Offset", roundedRectangle.Offset);
                        WriteOptional("Size", roundedRectangle.Size);
                        Write("CornerRadius", roundedRectangle.CornerRadius);
                        break;
                    case CompositionRectangleGeometry rectangle:
                        WriteOptional("Offset", rectangle.Offset);
                        WriteOptional("Size", rectangle.Size);
                        break;
                    case CompositionEllipseGeometry ellipse:
                        Write("Center", ellipse.Center);
                        Write("Radius", ellipse.Radius);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected geometry {value.Type}.");
                }

                _indent--;
            }

            public void WriteBrush(CompositionBrush? value)
            {
                if (WriteNodeHeader(value, "Brush"))
                {
                    return;
                }

                WriteCompositionObject(value!);

                switch (value)
                {
                    case CompositionColorBrush colorBrush:
                        WriteOptional("Color", colorBrush.Color);
                        break;
                    case CompositionMaskBrush maskBrush:
                        WriteChild("Source", maskBrush.Source, WriteBrush);
                        WriteChild("Mask", maskBrush.Mask, WriteBrush);
                        break;
                    case CompositionSurfaceBrush surfaceBrush:
                        WriteChild("Surface", surfaceBrush.Surface, WriteSurface);
                        break;
                    case CompositionEffectBrush effectBrush:
                        var effect = effectBrush.GetEffectFactory().Effect;
                        WriteChild("Effect", effect, WriteEffect);

                        foreach (var source in effect.Sources)
                        {
                            WriteChild(
                                $"Source({source.Name})",
                                effectBrush.GetSourceParameter(source.Name),
                                WriteBrush);
                        }

                        break;
                    case CompositionLinearGradientBrush _:
                    case CompositionRadialGradientBrush _:
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected brush {value!.Type}.");
                }

                if (value is CompositionGradientBrush gradientBrush)
                {
                    WriteOptional("AnchorPoint", gradientBrush.AnchorPoint);
                    WriteOptional("CenterPoint", gradientBrush.CenterPoint);
                    WriteList("ColorStops", gradientBrush.ColorStops, WriteGradientStop);
                    WriteOptional("ExtendMode", gradientBrush.ExtendMode);
                    WriteOptional("InterpolationSpace", gradientBrush.InterpolationSpace);
                    WriteOptional("MappingMode", gradientBrush.MappingMode);
                    WriteOptional("Offset", gradientBrush.Offset);
                    WriteOptional("RotationAngleInDegrees", gradientBrush.RotationAngleInDegrees);
                    WriteOptional("Scale", gradientBrush.Scale);
                    WriteOptional("TransformMatrix", gradientBrush.TransformMatrix);

                    switch (gradientBrush)
                    {
                        case CompositionLinearGradientBrush linear:
                            WriteOptional("StartPoint", linear.StartPoint);
                            WriteOptional("EndPoint", linear.EndPoint);
                            break;
                        case CompositionRadialGradientBrush radial:
                            WriteOptional("EllipseCenter", radial.EllipseCenter);
                            WriteOptional("EllipseRadius", radial.EllipseRadius);
                            WriteOptional("GradientOriginOffset", radial.GradientOriginOffset);
                            break;
                    }
                }

                _indent--;
            }

            public void WriteGradientStop(CompositionColorGradientStop? value)
            {
                if (WriteNodeHeader(value, "GradientStop"))
                {
                    return;
                }

                WriteCompositionObject(value!);
                Write("Color", value!.Color);
                Write("Offset", value.Offset);
                _indent--;
            }

            public void WriteViewBox(CompositionViewBox? value)
            {
                if (WriteNodeHeader(value, "ViewBox"))
                {
                    return;
                }

                WriteCompositionObject(value!);
                Write("Size", value!.Size);
                _indent--;
            }

            public void WriteClip(CompositionClip? value)
            {
                if (WriteNodeHeader(value, "Clip"))
                {
                    return;
                }

                WriteCompositionObject(value!);
                WriteOptional("CenterPoint", value!.CenterPoint);
                WriteOptional("Scale", value.Scale);

                switch (value)
                {
                    case InsetClip inset:
                        WriteOptional("LeftInset", inset.LeftInset);
                        WriteOptional("RightInset", inset.RightInset);
                        WriteOptional("TopInset", inset.TopInset);
                        WriteOptional("BottomInset", inset.BottomInset);
                        break;
                    case CompositionGeometricClip geometric:
                        WriteChild("Geometry", geometric.Geometry, WriteGeometry);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected clip {value.Type}.");
                }

                _indent--;
            }

            public void WriteShadow(CompositionShadow? value)
            {
                if (WriteNodeHeader(value, "Shadow"))
                {
                    return;
                }

                WriteCompositionObject(value!);

                switch (value)
                {
                    case DropShadow dropShadow:
                        WriteOptional("BlurRadius", dropShadow.BlurRadius);
                        WriteOptional("Color", dropShadow.Color);
                        WriteChild("Mask", dropShadow.Mask, WriteBrush);
                        WriteOptional("Offset", dropShadow.Offset);
                        WriteOptional("Opacity", dropShadow.Opacity);
                        WriteOptional("SourcePolicy", dropShadow.SourcePolicy);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected shadow {value!.Type}.");
                }

                _indent--;
            }

            public void WriteSurface(ICompositionSurface? value)
            {
                if (WriteNodeHeader(value, "Surface"))
                {
                    return;
                }

                switch (value)
                {
                    case CompositionVisualSurface visualSurface:
                        WriteCompositionObject(visualSurface);
                        WriteChild("SourceVisual", visualSurface.SourceVisual, WriteVisual);
                        WriteOptional("SourceSize", visualSurface.SourceSize);
                        WriteOptional("SourceOffset", visualSurface.SourceOffset);
                        break;
                    case LoadedImageSurfaceFromUri fromUri:
                        WriteLine($"Kind: FromUri");
                        WriteLine($"Uri: {fromUri.Uri}");
                        break;
                    case LoadedImageSurfaceFromStream fromStream:
                        WriteLine($"Kind: FromStream");
                        WriteLine($"Bytes: {Convert.ToBase64String(fromStream.Bytes)}");
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected surface {value!.GetType().Name}.");
                }

                _indent--;
            }

            public void WriteEffect(GraphicsEffectBase? value)
            {
                if (WriteNodeHeader(value, "Effect"))
                {
                    return;
                }

                WriteLine($"Sources: {string.Join(", ", value!.Sources.Select(s => s.Name))}");

                switch (value)
                {
                    case CompositeEffect composite:
                        Write("Mode", composite.Mode);
                        break;
                    case GaussianBlurEffect blur:
                        Write("BlurAmount", blur.BlurAmount);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected effect {value.Type}.");
                }

                _indent--;
            }

            public void WriteEasing(CompositionEasingFunction? value)
            {
                if (WriteNodeHeader(value, "Easing"))
                {
                    return;
                }

                WriteCompositionObject(value!);

                switch (value)
                {
                    case LinearEasingFunction _:
                        break;
                    case CubicBezierEasingFunction cubicBezier:
                        Write("ControlPoint1", cubicBezier.ControlPoint1);
                        Write("ControlPoint2", cubicBezier.ControlPoint2);
                        break;
                    case StepEasingFunction step:
                        WriteOptional("StepCount", step.StepCount);
                        WriteOptional("InitialStep", step.InitialStep);
                        WriteOptional("FinalStep", step.FinalStep);
                        WriteOptional("IsInitialStepSingleFrame", step.IsInitialStepSingleFrame);
                        WriteOptional("IsFinalStepSingleFrame", step.IsFinalStepSingleFrame);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected easing {value!.Type}.");
                }

                _indent--;
            }

            public void WriteAnimation(CompositionAnimation? value)
            {
                if (WriteNodeHeader(value, "Animation"))
                {
                    return;
                }

                WriteCompositionObject(value!);
                WriteLine($"Target: {Text(value!.Target)}");

                // ReferenceParameters is a dictionary, so it is sorted to make the dump
                // independent of hash ordering.
                foreach (var parameter in value.ReferenceParameters.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    WriteChild($"Reference({parameter.Key})", parameter.Value, WriteCompositionObjectReference);
                }

                switch (value)
                {
                    case ExpressionAnimation expression:
                        // Expressions are compared as text because that is how they are
                        // stored in the buffer, and how Windows.UI.Composition accepts them.
                        WriteLine($"Expression: {expression.Expression.ToText()}");
                        break;
                    case KeyFrameAnimation_ keyFrameAnimation:
                        WriteLine($"Duration: {keyFrameAnimation.Duration.Ticks}");

                        if (value is ColorKeyFrameAnimation color)
                        {
                            Write("InterpolationColorSpace", color.InterpolationColorSpace);
                        }

                        foreach (var keyFrame in keyFrameAnimation.KeyFrames)
                        {
                            WriteKeyFrame(keyFrame);
                        }

                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected animation {value.Type}.");
                }

                _indent--;
            }

            void WriteKeyFrame(KeyFrameAnimation_.KeyFrame value)
            {
                WriteLine($"KeyFrame @{Text(value.Progress)}");
                _indent++;
                WriteChild("Easing", value.Easing, WriteEasing);

                switch (value)
                {
                    case KeyFrameAnimation_.ExpressionKeyFrame expression:
                        WriteLine($"Expression: {expression.Expression.ToText()}");
                        break;
                    case KeyFrameAnimation<float, Expressions.Scalar>.ValueKeyFrame scalar:
                        WriteLine($"Value: {Text(scalar.Value)}");
                        break;
                    case KeyFrameAnimation<bool, Expressions.Boolean>.ValueKeyFrame boolean:
                        WriteLine($"Value: {boolean.Value}");
                        break;
                    case KeyFrameAnimation<Vector2, Expressions.Vector2>.ValueKeyFrame vector2:
                        WriteLine($"Value: {Text(vector2.Value)}");
                        break;
                    case KeyFrameAnimation<Vector3, Expressions.Vector3>.ValueKeyFrame vector3:
                        WriteLine($"Value: {Text(vector3.Value)}");
                        break;
                    case KeyFrameAnimation<Vector4, Expressions.Vector4>.ValueKeyFrame vector4:
                        WriteLine($"Value: {Text(vector4.Value)}");
                        break;
                    case KeyFrameAnimation<Wui.Color, Expressions.Color>.ValueKeyFrame color:
                        WriteLine($"Value: {Text(color.Value)}");
                        break;
                    case KeyFrameAnimation<CompositionPath, Expressions.Void>.ValueKeyFrame path:
                        WriteChild("Value", (CanvasGeometry)path.Value.Source, WriteCanvasGeometry);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected key frame {value.Type}.");
                }

                _indent--;
            }

            public void WriteCanvasGeometry(CanvasGeometry? value)
            {
                if (WriteNodeHeader(value, "CanvasGeometry"))
                {
                    return;
                }

                switch (value)
                {
                    case CanvasGeometry.Combination combination:
                        WriteChild("A", combination.A, WriteCanvasGeometry);
                        WriteChild("B", combination.B, WriteCanvasGeometry);
                        Write("CombineMode", combination.CombineMode);
                        Write("Matrix", combination.Matrix);
                        break;
                    case CanvasGeometry.TransformedGeometry transformed:
                        WriteChild("Source", transformed.SourceGeometry, WriteCanvasGeometry);
                        Write("TransformMatrix", transformed.TransformMatrix);
                        break;
                    case CanvasGeometry.Group group:
                        Write("FilledRegionDetermination", group.FilledRegionDetermination);
                        WriteList("Geometries", group.Geometries, WriteCanvasGeometry);
                        break;
                    case CanvasGeometry.Path path:
                        Write("FilledRegionDetermination", path.FilledRegionDetermination);

                        foreach (var command in path.Commands)
                        {
                            WriteLine(Text(command));
                        }

                        break;
                    case CanvasGeometry.Ellipse ellipse:
                        WriteLine(
                            $"Ellipse: {Text(ellipse.X)} {Text(ellipse.Y)} " +
                            $"{Text(ellipse.RadiusX)} {Text(ellipse.RadiusY)}");
                        break;
                    case CanvasGeometry.RoundedRectangle rectangle:
                        WriteLine(
                            $"RoundedRectangle: {Text(rectangle.X)} {Text(rectangle.Y)} " +
                            $"{Text(rectangle.W)} {Text(rectangle.H)} " +
                            $"{Text(rectangle.RadiusX)} {Text(rectangle.RadiusY)}");
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected canvas geometry {value!.Type}.");
                }

                _indent--;
            }

            void WriteCompositionObjectReference(CompositionObject? value)
            {
                switch (value)
                {
                    case null:
                        WriteLine("null");
                        break;
                    case Visual visual:
                        WriteVisual(visual);
                        break;
                    case CompositionShape shape:
                        WriteShape(shape);
                        break;
                    case CompositionGeometry geometry:
                        WriteGeometry(geometry);
                        break;
                    case CompositionBrush brush:
                        WriteBrush(brush);
                        break;
                    case CompositionAnimation animation:
                        WriteAnimation(animation);
                        break;
                    case CompositionEasingFunction easing:
                        WriteEasing(easing);
                        break;
                    case CompositionPropertySet propertySet:
                        WritePropertySet(propertySet);
                        break;
                    case CompositionVisualSurface surface:
                        WriteSurface(surface);
                        break;
                    case CompositionClip clip:
                        WriteClip(clip);
                        break;
                    case AnimationController controller:
                        WriteController(controller);
                        break;
                    case CompositionShadow shadow:
                        WriteShadow(shadow);
                        break;
                    case CompositionColorGradientStop stop:
                        WriteGradientStop(stop);
                        break;
                    case CompositionViewBox viewBox:
                        WriteViewBox(viewBox);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected reference to {value.Type}.");
                }
            }

            void WritePropertySet(CompositionPropertySet? value)
            {
                if (WriteNodeHeader(value, "PropertySet"))
                {
                    return;
                }

                WritePropertySetContents(value!);
                _indent--;
            }

            void WriteController(AnimationController? value)
            {
                if (WriteNodeHeader(value, "Controller"))
                {
                    return;
                }

                WriteLine($"IsCustom: {value!.IsCustom}");
                WriteLine($"IsPaused: {value.IsPaused}");
                WriteLine($"TargetProperty: {Text(value.TargetProperty)}");
                WriteCompositionObject(value);
                _indent--;
            }

            // Writes the state that every CompositionObject has. The property set is
            // written inline rather than as a node, because it belongs to the object.
            void WriteCompositionObject(CompositionObject value)
            {
                if (value.Comment is not null)
                {
                    WriteLine($"Comment: {value.Comment}");
                }

                // Properties is created on demand, so it is only written when the object
                // actually has some, which is exactly when it is serialized.
                var properties = value.Properties;
                if (value.Type != CompositionObjectType.CompositionPropertySet &&
                    (properties.Names.Count > 0 || properties.Animators.Any()))
                {
                    WriteLine("Properties");
                    _indent++;
                    WritePropertySetContents(properties);
                    _indent--;
                }

                foreach (var animator in value.Animators)
                {
                    WriteLine($"Animator: {animator.AnimatedProperty}");
                    _indent++;
                    WriteChild("Animation", animator.Animation, WriteAnimation);

                    if (animator.Controller is not null)
                    {
                        WriteChild("Controller", animator.Controller, WriteController);
                    }

                    _indent--;
                }
            }

            void WritePropertySetContents(CompositionPropertySet value)
            {
                // Names is a SortedDictionary, so this order is already canonical.
                foreach (var (name, type) in value.Names.Select(n => (n.Key, n.Value)))
                {
                    switch (type)
                    {
                        case PropertySetValueType.Color:
                            value.TryGetColor(name, out var color);
                            WriteLine($"{name}: {Text(color!.Value)}");
                            break;
                        case PropertySetValueType.Scalar:
                            value.TryGetScalar(name, out var scalar);
                            WriteLine($"{name}: {Text(scalar!.Value)}");
                            break;
                        case PropertySetValueType.Vector2:
                            value.TryGetVector2(name, out var vector2);
                            WriteLine($"{name}: {Text(vector2!.Value)}");
                            break;
                        case PropertySetValueType.Vector3:
                            value.TryGetVector3(name, out var vector3);
                            WriteLine($"{name}: {Text(vector3!.Value)}");
                            break;
                        case PropertySetValueType.Vector4:
                            value.TryGetVector4(name, out var vector4);
                            WriteLine($"{name}: {Text(vector4!.Value)}");
                            break;
                        default:
                            throw new InvalidOperationException($"Unexpected property {type}.");
                    }
                }

                foreach (var animator in value.Animators)
                {
                    WriteLine($"Animator: {animator.AnimatedProperty}");
                    _indent++;
                    WriteChild("Animation", animator.Animation, WriteAnimation);
                    _indent--;
                }
            }

            // Writes the "Kind #id" line that starts a node, and returns true if the node
            // has been written before, in which case the caller must not write it again.
            // Leaves the indent increased when it returns false.
            bool WriteNodeHeader(object? value, string category)
            {
                if (value is null)
                {
                    WriteLine($"{category}: null");
                    return true;
                }

                if (_ids.TryGetValue(value, out var existing))
                {
                    WriteLine($"{category} #{existing} (shared)");
                    return true;
                }

                var id = _ids.Count;
                _ids.Add(value, id);
                WriteLine($"{category} #{id} {TypeNameOf(value)}");
                _indent++;
                return false;
            }

            static string TypeNameOf(object value)
                => value switch
                {
                    CompositionObject compositionObject => compositionObject.Type.ToString(),
                    CanvasGeometry canvasGeometry => canvasGeometry.Type.ToString(),
                    GraphicsEffectBase effect => effect.Type.ToString(),
                    LoadedImageSurface surface => surface.Type.ToString(),
                    _ => value.GetType().Name,
                };

            void WriteChild<T>(string name, T? value, Action<T?> write)
                where T : class
            {
                WriteLine($"{name}:");
                _indent++;
                write(value);
                _indent--;
            }

            void WriteList<T>(string name, IEnumerable<T> values, Action<T?> write)
                where T : class
            {
                var array = values.ToArray();
                if (array.Length == 0)
                {
                    return;
                }

                WriteLine($"{name}: [{array.Length}]");
                _indent++;
                foreach (var value in array)
                {
                    write(value);
                }

                _indent--;
            }

            // Written only when the property has a value, so that "never set" and "set to
            // the default" stay distinguishable, exactly as they are in the buffer.
            void WriteOptional<T>(string name, T? value)
                where T : struct
            {
                if (value.HasValue)
                {
                    WriteLine($"{name}: {Text(value.Value)}");
                }
            }

            void Write<T>(string name, T value)
                where T : struct
                => WriteLine($"{name}: {Text(value)}");

            void WriteFloats(string name, IEnumerable<float> values)
            {
                var array = values.ToArray();
                if (array.Length > 0)
                {
                    WriteLine($"{name}: {string.Join(", ", array.Select(v => Text(v)))}");
                }
            }

            void WriteLine(string text)
                => _builder.Append(' ', _indent * 2).Append(text).Append('\n');

            // Values are formatted with round trip precision and the invariant culture, so
            // that the dump is exact and machine independent.
            static string Text(object? value)
                => value switch
                {
                    null => "null",
                    float f => f.ToString("R", CultureInfo.InvariantCulture),
                    Vector2 v => $"<{Text(v.X)}, {Text(v.Y)}>",
                    Vector3 v => $"<{Text(v.X)}, {Text(v.Y)}, {Text(v.Z)}>",
                    Vector4 v => $"<{Text(v.X)}, {Text(v.Y)}, {Text(v.Z)}, {Text(v.W)}>",
                    Matrix3x2 m =>
                        $"[{Text(m.M11)}, {Text(m.M12)}, {Text(m.M21)}, " +
                        $"{Text(m.M22)}, {Text(m.M31)}, {Text(m.M32)}]",
                    Matrix4x4 m =>
                        $"[{Text(m.M11)}, {Text(m.M12)}, {Text(m.M13)}, {Text(m.M14)}, " +
                        $"{Text(m.M21)}, {Text(m.M22)}, {Text(m.M23)}, {Text(m.M24)}, " +
                        $"{Text(m.M31)}, {Text(m.M32)}, {Text(m.M33)}, {Text(m.M34)}, " +
                        $"{Text(m.M41)}, {Text(m.M42)}, {Text(m.M43)}, {Text(m.M44)}]",
                    Wui.Color c => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}",
                    CanvasPathBuilder.Command.BeginFigure begin => $"BeginFigure {Text(begin.StartPoint)}",
                    CanvasPathBuilder.Command.EndFigure end => $"EndFigure {end.FigureLoop}",
                    CanvasPathBuilder.Command.AddLine line => $"AddLine {Text(line.EndPoint)}",
                    CanvasPathBuilder.Command.AddCubicBezier bezier =>
                        $"AddCubicBezier {Text(bezier.ControlPoint1)} " +
                        $"{Text(bezier.ControlPoint2)} {Text(bezier.EndPoint)}",
                    _ => value.ToString() ?? string.Empty,
                };
        }

        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            ReferenceEqualityComparer()
            {
            }

            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
