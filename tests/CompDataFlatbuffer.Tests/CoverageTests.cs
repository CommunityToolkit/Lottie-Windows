// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgc;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Xunit;
using Expr = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Wui = CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Verifies that every kind of node that a graph can contain survives serialization.
    /// </summary>
    /// <remarks>
    /// The sample animations exercise the parts of the object model that the translator
    /// happens to emit for them, which is not all of it. This builds a graph that
    /// contains at least one of every <see cref="CompositionObjectType"/>, every
    /// <see cref="CanvasGeometry"/> shape, every path command and every effect, and
    /// round trips that. Together with <see cref="AllCompositionObjectTypesAreCovered"/>
    /// this means a node type cannot be added to WinCompData without either being
    /// handled here or failing the build.
    /// </remarks>
    public class CoverageTests
    {
        [Fact]
        public void AllCompositionObjectTypesAreCovered()
        {
            var covered = Collect(BuildGraph())
                .Select(o => o.Type)
                .ToHashSet();

            var missing = Enum.GetValues<CompositionObjectType>()
                .Where(t => !covered.Contains(t))
                .ToArray();

            Assert.True(
                missing.Length == 0,
                $"The coverage graph does not contain: {string.Join(", ", missing)}.");
        }

        [Fact]
        public void EveryNodeTypeSurvivesARoundTrip()
        {
            var root = BuildGraph();

            var expected = CompositionTreeDumper.Dump(root);
            var actual = CompositionTreeDumper.Dump(
                CompositionDeserializer.Deserialize(CompositionSerializer.Serialize(root, 14, null, null, 100, 100)));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EveryNodeTypeIsInterpreted()
        {
            var root = BuildGraph();

            // The interpreter has a branch per node type just as the deserializer does,
            // so it is held to the same standard: every node type that a graph can
            // contain must come out of the buffer unchanged.
            var expected = CompositionTreeDumper.Dump(root);
            var actual = InterpretedTreeDumper.Dump(
                LottieRuntime.CompositionInterpreter.LoadComposition(
                    new Windows.UI.Composition.Compositor(),
                    CompositionSerializer.Serialize(root, 14, null, null, 100, 100)));

            Assert.Equal(expected, actual);
        }

        // Builds a graph containing one of everything. It is not a sensible animation;
        // its only job is to reach every branch of the serializer and the deserializer.
        static Visual BuildGraph()
        {
            var c = new Compositor();

            var root = c.CreateContainerVisual();
            root.Comment = "Root";
            root.Offset = new Vector3(1, 2, 3);
            root.CenterPoint = new Vector3(4, 5, 6);
            root.RotationAngleInDegrees = 45;
            root.RotationAxis = new Vector3(0, 0, 1);
            root.Scale = new Vector3(2, 2, 2);
            root.Size = new Vector2(100, 100);
            root.Opacity = 0.5f;
            root.IsVisible = true;
            root.BorderMode = CompositionBorderMode.Soft;
            root.TransformMatrix = Matrix4x4.Identity;

            // Clips. An inset clip on the root, a geometric clip further down.
            var insetClip = c.CreateInsetClip();
            insetClip.LeftInset = 1;
            insetClip.RightInset = 2;
            insetClip.TopInset = 3;
            insetClip.BottomInset = 4;
            root.Clip = insetClip;

            // Easings.
            var linear = c.CreateLinearEasingFunction();
            var cubic = c.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.2f), new Vector2(0.3f, 0.4f));
            var step = c.CreateStepEasingFunction(3);
            step.IsInitialStepSingleFrame = true;
            step.IsFinalStepSingleFrame = true;
            step.InitialStep = 1;
            step.FinalStep = 2;

            // Every kind of key frame animation, using every kind of easing and both
            // kinds of key frame (a value and an expression).
            var scalarAnimation = c.CreateScalarKeyFrameAnimation();
            scalarAnimation.Duration = TimeSpan.FromSeconds(1);
            scalarAnimation.InsertKeyFrame(0, 0, linear);
            scalarAnimation.InsertKeyFrame(0.5f, 1, cubic);
            scalarAnimation.InsertExpressionKeyFrame(1, Expr.Expression.Scalar("0"), step);

            var booleanAnimation = c.CreateBooleanKeyFrameAnimation();
            booleanAnimation.Duration = TimeSpan.FromSeconds(1);
            booleanAnimation.InsertKeyFrame(0, false);
            booleanAnimation.InsertKeyFrame(1, true);

            var colorAnimation = c.CreateColorKeyFrameAnimation();
            colorAnimation.Duration = TimeSpan.FromSeconds(1);
            colorAnimation.InterpolationColorSpace = CompositionColorSpace.Hsl;
            colorAnimation.InsertKeyFrame(0, Wui.Color.FromArgb(255, 1, 2, 3), linear);
            colorAnimation.InsertKeyFrame(1, Wui.Color.FromArgb(0, 4, 5, 6), linear);

            var vector2Animation = c.CreateVector2KeyFrameAnimation();
            vector2Animation.Duration = TimeSpan.FromSeconds(1);
            vector2Animation.InsertKeyFrame(0, new Vector2(1, 2), linear);

            var vector3Animation = c.CreateVector3KeyFrameAnimation();
            vector3Animation.Duration = TimeSpan.FromSeconds(1);
            vector3Animation.InsertKeyFrame(0, new Vector3(1, 2, 3), linear);

            var vector4Animation = c.CreateVector4KeyFrameAnimation();
            vector4Animation.Duration = TimeSpan.FromSeconds(1);
            vector4Animation.InsertKeyFrame(0, new Vector4(1, 2, 3, 4), linear);

            var pathAnimation = c.CreatePathKeyFrameAnimation();
            pathAnimation.Duration = TimeSpan.FromSeconds(1);
            pathAnimation.InsertKeyFrame(0, new CompositionPath(BuildPathGeometry()), linear);

            // A property set that other objects reference through expressions. This is
            // how the translator exposes theming properties.
            var theme = c.CreatePropertySet();
            theme.InsertScalar("Scalar", 1);
            theme.InsertVector2("Vector2", new Vector2(1, 2));
            theme.InsertVector3("Vector3", new Vector3(1, 2, 3));
            theme.InsertVector4("Vector4", new Vector4(1, 2, 3, 4));
            theme.InsertColor("Color", Wui.Color.FromArgb(255, 7, 8, 9));

            var expressionAnimation = c.CreateExpressionAnimation(Expr.Expression.Scalar("_.Scalar"));
            expressionAnimation.SetReferenceParameter("_", theme);

            // A custom controller, which is the only kind the format has to carry, since
            // a default one is recreated on the other side.
            var controller = c.CreateAnimationController();
            controller.Pause();

            // Shapes.
            var shapeVisual = c.CreateShapeVisual();
            shapeVisual.Size = new Vector2(100, 100);
            shapeVisual.ViewBox = c.CreateViewBox();
            shapeVisual.ViewBox.Size = new Vector2(50, 50);

            var containerShape = c.CreateContainerShape();
            containerShape.CenterPoint = new Vector2(1, 2);
            containerShape.Offset = new Vector2(3, 4);
            containerShape.RotationAngleInDegrees = 90;
            containerShape.Scale = new Vector2(2, 2);
            containerShape.TransformMatrix = Matrix3x2.Identity;
            shapeVisual.Shapes.Add(containerShape);

            // Every geometry type, each on its own sprite shape so that the sprite shape
            // properties are exercised as well.
            var ellipse = c.CreateEllipseGeometry();
            ellipse.Center = new Vector2(1, 2);
            ellipse.Radius = new Vector2(3, 4);
            ellipse.TrimStart = 0.1f;
            ellipse.TrimEnd = 0.9f;
            ellipse.TrimOffset = 0.05f;

            var rectangle = c.CreateRectangleGeometry();
            rectangle.Offset = new Vector2(1, 2);
            rectangle.Size = new Vector2(3, 4);

            var roundedRectangle = c.CreateRoundedRectangleGeometry();
            roundedRectangle.Offset = new Vector2(1, 2);
            roundedRectangle.Size = new Vector2(3, 4);
            roundedRectangle.CornerRadius = new Vector2(5, 6);

            var pathGeometry = c.CreatePathGeometry(new CompositionPath(BuildPathGeometry()));

            var colorBrush = c.CreateColorBrush(Wui.Color.FromArgb(255, 10, 20, 30));

            var linearGradient = c.CreateLinearGradientBrush();
            linearGradient.StartPoint = new Vector2(0, 0);
            linearGradient.EndPoint = new Vector2(1, 1);
            linearGradient.AnchorPoint = new Vector2(0.5f, 0.5f);
            linearGradient.CenterPoint = new Vector2(0.5f, 0.5f);
            linearGradient.ExtendMode = CompositionGradientExtendMode.Mirror;
            linearGradient.InterpolationSpace = CompositionColorSpace.HslLinear;
            linearGradient.ColorStops.Add(c.CreateColorGradientStop(0, Wui.Color.FromArgb(255, 0, 0, 0)));
            linearGradient.ColorStops.Add(c.CreateColorGradientStop(1, Wui.Color.FromArgb(255, 255, 255, 255)));

            var radialGradient = c.CreateRadialGradientBrush();
            radialGradient.EllipseCenter = new Vector2(0.5f, 0.5f);
            radialGradient.EllipseRadius = new Vector2(0.5f, 0.5f);
            radialGradient.GradientOriginOffset = new Vector2(0.1f, 0.1f);
            radialGradient.ColorStops.Add(c.CreateColorGradientStop(0, Wui.Color.FromArgb(255, 1, 2, 3)));

            var strokedShape = AddSpriteShape(c, containerShape, ellipse, colorBrush, linearGradient);
            strokedShape.StrokeDashArray.Add(1);
            strokedShape.StrokeDashArray.Add(2);
            strokedShape.StrokeDashOffset = 3;
            strokedShape.StrokeDashCap = CompositionStrokeCap.Round;
            strokedShape.StrokeStartCap = CompositionStrokeCap.Square;
            strokedShape.StrokeEndCap = CompositionStrokeCap.Triangle;
            strokedShape.StrokeLineJoin = CompositionStrokeLineJoin.Bevel;
            strokedShape.StrokeMiterLimit = 4;
            strokedShape.StrokeThickness = 2;
            strokedShape.IsStrokeNonScaling = true;

            AddSpriteShape(c, containerShape, rectangle, colorBrush, radialGradient);
            AddSpriteShape(c, containerShape, roundedRectangle, colorBrush, null);
            AddSpriteShape(c, containerShape, pathGeometry, colorBrush, null);

            // A geometric clip, which is the other consumer of a geometry.
            var geometricClip = c.CreateGeometricClip();
            geometricClip.Geometry = pathGeometry;
            shapeVisual.Clip = geometricClip;

            // A layer visual with a drop shadow.
            var layerVisual = c.CreateLayerVisual();
            layerVisual.Size = new Vector2(100, 100);

            var shadow = c.CreateDropShadow();
            shadow.BlurRadius = 5;
            shadow.Color = Wui.Color.FromArgb(128, 0, 0, 0);
            shadow.Offset = new Vector3(1, 2, 0);
            shadow.Opacity = 0.5f;
            shadow.Mask = colorBrush;
            shadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;
            layerVisual.Shadow = shadow;
            layerVisual.Children.Add(shapeVisual);

            // A visual surface, a surface brush over it, and a mask brush combining two
            // brushes. These are how the translator implements masking.
            var visualSurface = c.CreateVisualSurface();
            visualSurface.SourceVisual = shapeVisual;
            visualSurface.SourceSize = new Vector2(100, 100);
            visualSurface.SourceOffset = new Vector2(1, 2);

            var surfaceBrush = c.CreateSurfaceBrush(visualSurface);

            // A surface brush over a loaded image, which is the other kind of surface.
            var imageBrush = c.CreateSurfaceBrush(
                LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///image.png")));

            var streamBrush = c.CreateSurfaceBrush(
                LoadedImageSurface.StartLoadFromStream(new byte[] { 1, 2, 3, 4 }));

            var maskBrush = c.CreateMaskBrush();
            maskBrush.Source = surfaceBrush;
            maskBrush.Mask = imageBrush;

            // Effects. Both effect types, reached through an effect factory and an
            // effect brush.
            var compositeEffect = new CompositeEffect(
                CanvasComposite.DestinationIn,
                new List<CompositionEffectSourceParameter>
                {
                    new CompositionEffectSourceParameter("source"),
                    new CompositionEffectSourceParameter("mask"),
                });

            var compositeBrush = c.CreateEffectFactory(compositeEffect).CreateBrush();
            compositeBrush.SetSourceParameter("source", surfaceBrush);
            compositeBrush.SetSourceParameter("mask", streamBrush);

            var blurEffect = new GaussianBlurEffect(3, new CompositionEffectSourceParameter("source"));
            var blurBrush = c.CreateEffectFactory(blurEffect).CreateBrush();
            blurBrush.SetSourceParameter("source", maskBrush);

            var effectVisual = c.CreateSpriteVisual();
            effectVisual.Size = new Vector2(100, 100);
            effectVisual.Brush = blurBrush;

            var compositeVisual = c.CreateSpriteVisual();
            compositeVisual.Size = new Vector2(100, 100);
            compositeVisual.Brush = compositeBrush;

            root.Children.Add(layerVisual);
            root.Children.Add(effectVisual);
            root.Children.Add(compositeVisual);

            // Bind an animation of every kind to something, so that the animators and
            // their controllers are all reachable.
            root.StartAnimation("Opacity", scalarAnimation, controller);
            root.StartAnimation("IsVisible", booleanAnimation);
            root.StartAnimation("Offset", vector3Animation);
            root.StartAnimation("Size", vector2Animation);
            root.StartAnimation("RotationAngleInDegrees", expressionAnimation);
            colorBrush.StartAnimation("Color", colorAnimation);
            pathGeometry.StartAnimation("Path", pathAnimation);
            linearGradient.ColorStops[0].StartAnimation("Offset", scalarAnimation);
            radialGradient.StartAnimation("EllipseCenter", vector2Animation);
            strokedShape.StartAnimation("StrokeThickness", scalarAnimation);
            theme.StartAnimation("Vector4", vector4Animation);

            return root;
        }

        static CompositionSpriteShape AddSpriteShape(
            Compositor compositor,
            CompositionContainerShape parent,
            CompositionGeometry geometry,
            CompositionBrush fill,
            CompositionBrush? stroke)
        {
            var shape = compositor.CreateSpriteShape();
            shape.Geometry = geometry;
            shape.FillBrush = fill;
            shape.StrokeBrush = stroke;
            parent.Shapes.Add(shape);
            return shape;
        }

        // Builds a geometry that contains every CanvasGeometry shape and every path
        // command, so that the flattened path encoding is fully exercised.
        static CanvasGeometry BuildPathGeometry()
        {
            using var builder = new CanvasPathBuilder(null);
            builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);
            builder.BeginFigure(new Vector2(0, 0));
            builder.AddLine(new Vector2(1, 0));
            builder.AddCubicBezier(new Vector2(2, 0), new Vector2(3, 1), new Vector2(4, 1));
            builder.EndFigure(CanvasFigureLoop.Closed);
            builder.BeginFigure(new Vector2(5, 5));
            builder.AddLine(new Vector2(6, 6));
            builder.EndFigure(CanvasFigureLoop.Open);

            var path = CanvasGeometry.CreatePath(builder);
            var ellipse = CanvasGeometry.CreateEllipse(null, 1, 2, 3, 4);
            var roundedRectangle = CanvasGeometry.CreateRoundedRectangle(null, 1, 2, 3, 4, 5, 6);
            var transformed = ellipse.Transform(Matrix3x2.CreateScale(2));
            var combination = path.CombineWith(
                roundedRectangle,
                Matrix3x2.Identity,
                CanvasGeometryCombine.Union);

            return CanvasGeometry.CreateGroup(
                null,
                new[] { combination, transformed },
                CanvasFilledRegionDetermination.Alternate);
        }

        // Walks every CompositionObject reachable from a root. This deliberately does not
        // reuse the serializer's walker, so that a node the serializer cannot see is
        // still counted as uncovered.
        static IEnumerable<CompositionObject> Collect(Visual root)
        {
            var seen = new HashSet<CompositionObject>();
            var result = new List<CompositionObject>();

            void Add(CompositionObject? o)
            {
                if (o is null || !seen.Add(o))
                {
                    return;
                }

                result.Add(o);

                Add(o.Properties);

                foreach (var animator in o.Animators)
                {
                    Add(animator.Animation);
                    Add(animator.Controller);
                }

                if (o is CompositionAnimation animation)
                {
                    foreach (var parameter in animation.ReferenceParameters)
                    {
                        Add(parameter.Value);
                    }

                    if (o is KeyFrameAnimation_ keyFrameAnimation)
                    {
                        foreach (var easing in keyFrameAnimation.KeyFrames.Select(f => f.Easing))
                        {
                            Add(easing);
                        }
                    }
                }

                switch (o)
                {
                    case SpriteVisual sprite:
                        Add(sprite.Brush);
                        Add(sprite.Clip);
                        foreach (var child in sprite.Children)
                        {
                            Add(child);
                        }

                        break;
                    case ContainerVisual container:
                        foreach (var child in container.Children)
                        {
                            Add(child);
                        }

                        Add(container.Clip);

                        if (container is LayerVisual layer)
                        {
                            Add(layer.Shadow);
                        }
                        else if (container is ShapeVisual shapeVisual)
                        {
                            Add(shapeVisual.ViewBox);
                            foreach (var shape in shapeVisual.Shapes)
                            {
                                Add(shape);
                            }
                        }

                        break;
                    case CompositionContainerShape containerShape:
                        foreach (var shape in containerShape.Shapes)
                        {
                            Add(shape);
                        }

                        break;
                    case CompositionSpriteShape spriteShape:
                        Add(spriteShape.Geometry);
                        Add(spriteShape.FillBrush);
                        Add(spriteShape.StrokeBrush);
                        break;
                    case CompositionGeometricClip geometricClip:
                        Add(geometricClip.Geometry);
                        break;
                    case CompositionGradientBrush gradient:
                        foreach (var stop in gradient.ColorStops)
                        {
                            Add(stop);
                        }

                        break;
                    case CompositionMaskBrush mask:
                        Add(mask.Source);
                        Add(mask.Mask);
                        break;
                    case CompositionSurfaceBrush surfaceBrush:
                        Add(surfaceBrush.Surface as CompositionObject);
                        break;
                    case CompositionEffectBrush effectBrush:
                        var effectFactory = effectBrush.GetEffectFactory();
                        Add(effectFactory);
                        foreach (var source in effectFactory.Effect.Sources)
                        {
                            Add(effectBrush.GetSourceParameter(source.Name));
                        }

                        break;
                    case CompositionVisualSurface visualSurface:
                        Add(visualSurface.SourceVisual);
                        break;
                    case DropShadow dropShadow:
                        Add(dropShadow.Mask);
                        break;
                }
            }

            Add(root);
            return result;
        }
    }
}
