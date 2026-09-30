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
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Graphics.Effects;
using Windows.UI.Composition;
using Windows.UI.Xaml.Media;
using Mgce = Microsoft.Graphics.Canvas.Effects;
using Wui = Windows.UI;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Renders a tree of composition objects as canonical text.
    /// </summary>
    /// <remarks>
    /// This is <see cref="CompositionTreeDumper"/> for the tree that
    /// <c>CompositionInterpreter</c> builds. It writes the same text for the same
    /// animation, which is what lets the interpreter be compared against the
    /// WinCompData graph that the buffer was written from: the graph and the
    /// interpreted tree are different object models, but if the two dumps are equal
    /// then the interpreter created the same objects, gave them the same values, and
    /// shared them in the same way.
    /// <para/>
    /// The two dumpers are deliberately not factored into one. Each is a plain
    /// description of one object model, and keeping them separate means neither can
    /// paper over a difference by describing both models in the same terms.
    /// </remarks>
    static class InterpretedTreeDumper
    {
        /// <summary>
        /// Renders an interpreted tree as canonical text.
        /// </summary>
        /// <param name="root">The root of the tree.</param>
        /// <returns>The canonical text of the tree.</returns>
        public static string Dump(Visual root)
        {
            var writer = new Writer();
            writer.WriteVisual(root);
            return writer.ToString();
        }

        sealed class Writer
        {
            readonly StringBuilder _builder = new StringBuilder();

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
                    WriteList("Children", container.Children.Visuals, WriteVisual);
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
                        throw new InvalidOperationException($"Unexpected visual {value.GetType().Name}.");
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
                        throw new InvalidOperationException($"Unexpected shape {value.GetType().Name}.");
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
                        WriteChild("Path", pathGeometry.Path?.Source, WriteCanvasGeometry);
                        break;
                    case CompositionRoundedRectangleGeometry roundedRectangle:
                        WriteOptional("Offset", roundedRectangle.Offset);
                        WriteOptional("Size", roundedRectangle.Size);
                        Write("CornerRadius", roundedRectangle.CornerRadius ?? default);
                        break;
                    case CompositionRectangleGeometry rectangle:
                        WriteOptional("Offset", rectangle.Offset);
                        WriteOptional("Size", rectangle.Size);
                        break;
                    case CompositionEllipseGeometry ellipse:
                        Write("Center", ellipse.Center ?? default);
                        Write("Radius", ellipse.Radius ?? default);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected geometry {value.GetType().Name}.");
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

                        foreach (var source in SourcesOf(effect))
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
                        throw new InvalidOperationException($"Unexpected brush {value!.GetType().Name}.");
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
                Write("Color", value!.Color ?? default);
                Write("Offset", value.Offset ?? default);
                _indent--;
            }

            public void WriteViewBox(CompositionViewBox? value)
            {
                if (WriteNodeHeader(value, "ViewBox"))
                {
                    return;
                }

                WriteCompositionObject(value!);
                Write("Size", value!.Size ?? default);
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
                        throw new InvalidOperationException($"Unexpected clip {value.GetType().Name}.");
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
                        throw new InvalidOperationException($"Unexpected shadow {value!.GetType().Name}.");
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

            public void WriteEffect(IGraphicsEffect? value)
            {
                if (WriteNodeHeader(value, "Effect"))
                {
                    return;
                }

                WriteLine($"Sources: {string.Join(", ", SourcesOf(value!).Select(s => s.Name))}");

                switch (value)
                {
                    case Mgce.CompositeEffect composite:
                        Write("Mode", composite.Mode);
                        break;
                    case Mgce.GaussianBlurEffect blur:
                        Write("BlurAmount", blur.BlurAmount);
                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected effect {value!.GetType().Name}.");
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
                        throw new InvalidOperationException($"Unexpected easing {value!.GetType().Name}.");
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

                foreach (var parameter in value.ReferenceParameters.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    WriteChild($"Reference({parameter.Key})", parameter.Value, WriteCompositionObjectReference);
                }

                switch (value)
                {
                    case ExpressionAnimation expression:
                        WriteLine($"Expression: {expression.Expression}");
                        break;
                    case KeyFrameAnimation keyFrameAnimation:
                        WriteLine($"Duration: {keyFrameAnimation.Duration.Ticks}");

                        if (value is ColorKeyFrameAnimation color)
                        {
                            Write("InterpolationColorSpace", color.InterpolationColorSpace ?? CompositionColorSpace.Auto);
                        }

                        foreach (var keyFrame in keyFrameAnimation.KeyFrames)
                        {
                            WriteKeyFrame(keyFrame);
                        }

                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected animation {value.GetType().Name}.");
                }

                _indent--;
            }

            void WriteKeyFrame(KeyFrameAnimation.KeyFrame value)
            {
                WriteLine($"KeyFrame @{Text(value.Progress)}");
                _indent++;
                WriteChild("Easing", value.Easing, WriteEasing);

                switch (value)
                {
                    case KeyFrameAnimation.ExpressionKeyFrame expression:
                        WriteLine($"Expression: {expression.Expression}");
                        break;
                    case KeyFrameAnimation.ValueKeyFrame valueKeyFrame:
                        if (valueKeyFrame.Value is CompositionPath path)
                        {
                            WriteChild("Value", path.Source, WriteCanvasGeometry);
                        }
                        else
                        {
                            WriteLine($"Value: {Text(valueKeyFrame.Value)}");
                        }

                        break;
                    default:
                        throw new InvalidOperationException($"Unexpected key frame {value.GetType().Name}.");
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
                        throw new InvalidOperationException($"Unexpected canvas geometry {value!.GetType().Name}.");
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
                        throw new InvalidOperationException($"Unexpected reference to {value.GetType().Name}.");
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

            void WriteCompositionObject(CompositionObject value)
            {
                if (value.Comment is not null)
                {
                    WriteLine($"Comment: {value.Comment}");
                }

                var properties = value.Properties;
                if (value is not CompositionPropertySet &&
                    (properties.Names.Count > 0 || properties.Animators.Count > 0))
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
                foreach (var name in value.Names.Keys)
                {
                    WriteLine($"{name}: {Text(value.GetValue(name))}");
                }

                foreach (var animator in value.Animators)
                {
                    WriteLine($"Animator: {animator.AnimatedProperty}");
                    _indent++;
                    WriteChild("Animation", animator.Animation, WriteAnimation);
                    _indent--;
                }
            }

            // The sources of an effect, in the order that they were given to it.
            static IEnumerable<CompositionEffectSourceParameter> SourcesOf(IGraphicsEffect effect)
                => effect switch
                {
                    Mgce.CompositeEffect composite => composite.Sources.Cast<CompositionEffectSourceParameter>(),
                    Mgce.GaussianBlurEffect blur => new[] { (CompositionEffectSourceParameter)blur.Source! },
                    _ => throw new InvalidOperationException($"Unexpected effect {effect.GetType().Name}."),
                };

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

            // The names of the stand-in types are the names of the real composition
            // types, which are also the names of the WinCompData types, so the two
            // dumps name the same object identically.
            static string TypeNameOf(object value)
                => value switch
                {
                    LoadedImageSurfaceFromUri _ => "FromUri",
                    LoadedImageSurfaceFromStream _ => "FromStream",
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
