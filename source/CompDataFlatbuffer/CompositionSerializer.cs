// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.CompMetadata;
using CommunityToolkit.WinUI.Lottie.LottieMetadata;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using CommunityToolkit.WinUI.Lottie.WinCompData.MetaData;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg;
using CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;
using Google.FlatBuffers;
using Expressions = CommunityToolkit.WinUI.Lottie.WinCompData.Expressions;
using Fb = CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Schema;
using Wui = CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer
{
    /// <summary>
    /// Serializes a translated Lottie animation - a WinCompData object graph - into the
    /// FlatBuffers format described by lottie_comp.fbs.
    /// </summary>
    /// <remarks>
    /// The output is deterministic: serializing the same graph twice produces byte
    /// identical results.
    /// </remarks>
#if PUBLIC_CompDataFlatbuffer
    public
#endif
    static class CompositionSerializer
    {
        /// <summary>
        /// Serializes a WinCompData graph into a FlatBuffer.
        /// </summary>
        /// <param name="root">The root visual of the graph.</param>
        /// <param name="requiredUapVersion">The minimum UAP version required to instantiate the graph.</param>
        /// <param name="metadata">Metadata describing the source Lottie animation, or null.</param>
        /// <param name="propertyBindings">The property bindings exposed by the graph, or null.</param>
        /// <param name="width">The width of the source Lottie animation.</param>
        /// <param name="height">The height of the source Lottie animation.</param>
        /// <returns>The serialized graph.</returns>
        public static byte[] Serialize(
            Visual root,
            ushort requiredUapVersion,
            LottieCompositionMetadata? metadata = null,
            IReadOnlyList<PropertyBinding>? propertyBindings = null,
            float width = 0,
            float height = 0)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var graph = GraphIndexer.Index(root);

            // 1024 is a starting size only - the builder grows as needed.
            var builder = new FlatBufferBuilder(1024);

            // Metadata strings are interned before the table is written, because the
            // metadata is serialized last but its strings must be in the table.
            InternMetadataStrings(graph, metadata, propertyBindings);

            // Strings are created first because a string cannot be created while a table
            // is under construction, and almost every table refers to one.
            var strings = new StringOffset[graph.StringList.Count];
            for (var i = 0; i < strings.Length; i++)
            {
                strings[i] = builder.CreateString(graph.StringList[i]);
            }

            // From here on, interning a new string would produce an index past the end of
            // the table that was just written, so the table is closed to additions.
            graph.FreezeStrings();

            // The node lists grow as the graph is walked, so they are indexed by position
            // rather than iterated, and the walk is complete before serialization starts.
            var visuals = Map(graph.VisualList, v => WriteVisual(builder, graph, v));
            var shapes = Map(graph.ShapeList, v => WriteShape(builder, graph, v));
            var geometries = Map(graph.GeometryList, v => WriteGeometry(builder, graph, v));
            var canvasGeometries = Map(graph.CanvasGeometryList, v => WriteCanvasGeometry(builder, graph, v));
            var brushes = Map(graph.BrushList, v => WriteBrush(builder, graph, v));
            var gradientStops = Map(graph.GradientStopList, v => WriteGradientStop(builder, graph, v));
            var viewBoxes = Map(graph.ViewBoxList, v => WriteViewBox(builder, graph, v));
            var clips = Map(graph.ClipList, v => WriteClip(builder, graph, v));
            var shadows = Map(graph.ShadowList, v => WriteShadow(builder, graph, v));
            var surfaces = Map(graph.SurfaceList, v => WriteSurface(builder, graph, v, strings));
            var effects = Map(graph.EffectList, v => WriteEffect(builder, graph, v));
            var easings = Map(graph.EasingList, v => WriteEasing(builder, graph, v));
            var animations = Map(graph.AnimationList, v => WriteAnimation(builder, graph, v));
            var propertySets = Map(graph.PropertySetList, v => WritePropertySet(builder, graph, v));
            var controllers = Map(graph.ControllerList, v => WriteController(builder, graph, v));

            var metadataOffset = WriteMetadata(builder, graph, metadata, propertyBindings, width, height);

            // The indices of the controllers that a host must supply, in index order.
            var customControllers = graph.ControllerList
                .Select((controller, index) => (controller, index))
                .Where(x => x.controller.IsCustom)
                .Select(x => (uint)x.index)
                .ToArray();

            var stringsVector = Fb.LottieComposition.CreateStringsVector(builder, strings);
            var visualsVector = Fb.LottieComposition.CreateVisualsVector(builder, visuals);
            var shapesVector = Fb.LottieComposition.CreateShapesVector(builder, shapes);
            var geometriesVector = Fb.LottieComposition.CreateGeometriesVector(builder, geometries);
            var canvasGeometriesVector = Fb.LottieComposition.CreateCanvasGeometriesVector(builder, canvasGeometries);
            var brushesVector = Fb.LottieComposition.CreateBrushesVector(builder, brushes);
            var gradientStopsVector = Fb.LottieComposition.CreateGradientStopsVector(builder, gradientStops);
            var viewBoxesVector = Fb.LottieComposition.CreateViewBoxesVector(builder, viewBoxes);
            var clipsVector = Fb.LottieComposition.CreateClipsVector(builder, clips);
            var shadowsVector = Fb.LottieComposition.CreateShadowsVector(builder, shadows);
            var surfacesVector = Fb.LottieComposition.CreateSurfacesVector(builder, surfaces);
            var effectsVector = Fb.LottieComposition.CreateEffectsVector(builder, effects);
            var easingsVector = Fb.LottieComposition.CreateEasingsVector(builder, easings);
            var animationsVector = Fb.LottieComposition.CreateAnimationsVector(builder, animations);
            var propertySetsVector = Fb.LottieComposition.CreatePropertySetsVector(builder, propertySets);
            var controllersVector = Fb.LottieComposition.CreateControllersVector(builder, controllers);
            var customControllersVector = Fb.LottieComposition.CreateCustomControllersVector(builder, customControllers);

            Fb.LottieComposition.StartLottieComposition(builder);
            Fb.LottieComposition.AddSchemaVersion(builder, Format.Version);
            Fb.LottieComposition.AddRequiredUapVersion(builder, requiredUapVersion);
            Fb.LottieComposition.AddMetadata(builder, metadataOffset);
            Fb.LottieComposition.AddRootVisual(builder, graph.GetVisual(root));
            Fb.LottieComposition.AddStrings(builder, stringsVector);
            Fb.LottieComposition.AddVisuals(builder, visualsVector);
            Fb.LottieComposition.AddShapes(builder, shapesVector);
            Fb.LottieComposition.AddGeometries(builder, geometriesVector);
            Fb.LottieComposition.AddCanvasGeometries(builder, canvasGeometriesVector);
            Fb.LottieComposition.AddBrushes(builder, brushesVector);
            Fb.LottieComposition.AddGradientStops(builder, gradientStopsVector);
            Fb.LottieComposition.AddViewBoxes(builder, viewBoxesVector);
            Fb.LottieComposition.AddClips(builder, clipsVector);
            Fb.LottieComposition.AddShadows(builder, shadowsVector);
            Fb.LottieComposition.AddSurfaces(builder, surfacesVector);
            Fb.LottieComposition.AddEffects(builder, effectsVector);
            Fb.LottieComposition.AddEasings(builder, easingsVector);
            Fb.LottieComposition.AddAnimations(builder, animationsVector);
            Fb.LottieComposition.AddPropertySets(builder, propertySetsVector);
            Fb.LottieComposition.AddControllers(builder, controllersVector);
            Fb.LottieComposition.AddCustomControllers(builder, customControllersVector);
            var composition = Fb.LottieComposition.EndLottieComposition(builder);

            Fb.LottieComposition.FinishLottieCompositionBuffer(builder, composition);

            return builder.SizedByteArray();
        }

        // Interns every string that WriteMetadata will need. Kept adjacent to
        // WriteMetadata so that the two stay in step.
        static void InternMetadataStrings(
            GraphIndexer graph,
            LottieCompositionMetadata? metadata,
            IReadOnlyList<PropertyBinding>? propertyBindings)
        {
            graph.GetString(metadata?.CompositionName);

            if (metadata is not null)
            {
                foreach (var marker in metadata.Markers)
                {
                    graph.GetString(marker.Name);
                }
            }

            if (propertyBindings is not null)
            {
                foreach (var binding in propertyBindings)
                {
                    graph.GetString(binding.BindingName);
                    graph.GetString(binding.DisplayName);
                }
            }
        }

        static TResult[] Map<T, TResult>(List<T> source, Func<T, TResult> selector)
        {
            // The list is indexed rather than enumerated because writing a node can
            // never add to it - the graph walk completed before serialization started.
            var result = new TResult[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = selector(source[i]);
            }

            return result;
        }

        // ---------------------------------------------------------------------
        // Shared state.
        // ---------------------------------------------------------------------
        // Writes the CompositionObject state shared by every node, or returns a null
        // offset if the object carries none of it.
        static Offset<Fb.CompObj> WriteCompObj(FlatBufferBuilder builder, GraphIndexer graph, CompositionObject value)
        {
            var comment = graph.GetString(value.Comment);

            var animators = value.Animators.ToArray();
            var animatorOffsets = new Offset<Fb.Animator>[animators.Length];
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                Fb.Animator.StartAnimator(builder);
                Fb.Animator.AddProperty(builder, graph.GetString(animator.AnimatedProperty));
                Fb.Animator.AddAnimation(builder, graph.GetAnimation(animator.Animation));
                Fb.Animator.AddController(builder, graph.GetController(animator.Controller));
                animatorOffsets[i] = Fb.Animator.EndAnimator(builder);
            }

            // The property set of a property set is itself, so it is never written as a
            // nested reference.
            var properties = value.Type != CompositionObjectType.CompositionPropertySet && GraphIndexer.HasState(value.Properties)
                ? graph.GetPropertySet(value.Properties)
                : Format.NullIndex;

            if (comment == Format.NullIndex && animators.Length == 0 && properties == Format.NullIndex)
            {
                return default;
            }

            var animatorsVector = animators.Length == 0
                ? default
                : Fb.CompObj.CreateAnimatorsVector(builder, animatorOffsets);

            Fb.CompObj.StartCompObj(builder);
            Fb.CompObj.AddComment(builder, comment);

            if (animators.Length > 0)
            {
                Fb.CompObj.AddAnimators(builder, animatorsVector);
            }

            Fb.CompObj.AddProperties(builder, properties);
            return Fb.CompObj.EndCompObj(builder);
        }

        static void AddBase(FlatBufferBuilder builder, Offset<Fb.CompObj> value, Action<FlatBufferBuilder, Offset<Fb.CompObj>> add)
        {
            if (value.Value != 0)
            {
                add(builder, value);
            }
        }

        // ---------------------------------------------------------------------
        // Nodes.
        // ---------------------------------------------------------------------
        static Offset<Fb.Visual> WriteVisual(FlatBufferBuilder builder, GraphIndexer graph, Visual value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            var children = value is ContainerVisual container && container.Children.Count > 0
                ? Fb.Visual.CreateChildrenVector(builder, container.Children.Select(graph.GetVisual).ToArray())
                : default;

            var shapes = value is ShapeVisual shapeVisual && shapeVisual.Shapes.Count > 0
                ? Fb.Visual.CreateShapesVector(builder, shapeVisual.Shapes.Select(graph.GetShape).ToArray())
                : default;

            Fb.Visual.StartVisual(builder);
            AddBase(builder, compObj, Fb.Visual.AddBase);
            Fb.Visual.AddKind(builder, value switch
            {
                ShapeVisual _ => Fb.VisualKind.Shape,
                SpriteVisual _ => Fb.VisualKind.Sprite,
                LayerVisual _ => Fb.VisualKind.Layer,
                _ => Fb.VisualKind.Container,
            });

            Fb.Visual.AddBorderMode(builder, (byte?)value.BorderMode);
            AddVec3(builder, value.CenterPoint, Fb.Visual.AddCenterPoint);
            Fb.Visual.AddClip(builder, graph.GetClip(value.Clip));
            Fb.Visual.AddIsVisible(builder, value.IsVisible);
            AddVec3(builder, value.Offset, Fb.Visual.AddOffset);
            Fb.Visual.AddOpacity(builder, value.Opacity);
            Fb.Visual.AddRotationAngleInDegrees(builder, value.RotationAngleInDegrees);
            AddVec3(builder, value.RotationAxis, Fb.Visual.AddRotationAxis);
            AddVec3(builder, value.Scale, Fb.Visual.AddScale);
            AddVec2(builder, value.Size, Fb.Visual.AddSize);
            AddMat4x4(builder, value.TransformMatrix, Fb.Visual.AddTransformMatrix);

            if (children.Value != 0)
            {
                Fb.Visual.AddChildren(builder, children);
            }

            if (shapes.Value != 0)
            {
                Fb.Visual.AddShapes(builder, shapes);
            }

            switch (value)
            {
                case ShapeVisual shape:
                    Fb.Visual.AddViewBox(builder, graph.GetViewBox(shape.ViewBox));
                    break;
                case SpriteVisual sprite:
                    Fb.Visual.AddBrush(builder, graph.GetBrush(sprite.Brush));
                    Fb.Visual.AddShadow(builder, graph.GetShadow(sprite.Shadow));
                    break;
                case LayerVisual layer:
                    Fb.Visual.AddShadow(builder, graph.GetShadow(layer.Shadow));
                    break;
            }

            return Fb.Visual.EndVisual(builder);
        }

        static Offset<Fb.Shape> WriteShape(FlatBufferBuilder builder, GraphIndexer graph, CompositionShape value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            var shapes = value is CompositionContainerShape containerShape && containerShape.Shapes.Count > 0
                ? Fb.Shape.CreateShapesVector(builder, containerShape.Shapes.Select(graph.GetShape).ToArray())
                : default;

            var spriteShape = value as CompositionSpriteShape;

            var strokeDashArray = spriteShape is not null && spriteShape.StrokeDashArray.Count > 0
                ? Fb.Shape.CreateStrokeDashArrayVector(builder, spriteShape.StrokeDashArray.ToArray())
                : default;

            Fb.Shape.StartShape(builder);
            AddBase(builder, compObj, Fb.Shape.AddBase);
            Fb.Shape.AddKind(builder, spriteShape is not null ? Fb.ShapeKind.Sprite : Fb.ShapeKind.Container);

            AddVec2(builder, value.CenterPoint, Fb.Shape.AddCenterPoint);
            AddVec2(builder, value.Offset, Fb.Shape.AddOffset);
            Fb.Shape.AddRotationAngleInDegrees(builder, value.RotationAngleInDegrees);
            AddVec2(builder, value.Scale, Fb.Shape.AddScale);
            AddMat3x2(builder, value.TransformMatrix, Fb.Shape.AddTransformMatrix);

            if (shapes.Value != 0)
            {
                Fb.Shape.AddShapes(builder, shapes);
            }

            if (spriteShape is not null)
            {
                Fb.Shape.AddFillBrush(builder, graph.GetBrush(spriteShape.FillBrush));
                Fb.Shape.AddStrokeBrush(builder, graph.GetBrush(spriteShape.StrokeBrush));
                Fb.Shape.AddGeometry(builder, graph.GetGeometry(spriteShape.Geometry));
                Fb.Shape.AddIsStrokeNonScaling(builder, spriteShape.IsStrokeNonScaling);
                Fb.Shape.AddStrokeDashOffset(builder, spriteShape.StrokeDashOffset);

                if (strokeDashArray.Value != 0)
                {
                    Fb.Shape.AddStrokeDashArray(builder, strokeDashArray);
                }

                Fb.Shape.AddStrokeDashCap(builder, (byte?)spriteShape.StrokeDashCap);
                Fb.Shape.AddStrokeStartCap(builder, (byte?)spriteShape.StrokeStartCap);
                Fb.Shape.AddStrokeEndCap(builder, (byte?)spriteShape.StrokeEndCap);
                Fb.Shape.AddStrokeLineJoin(builder, (byte?)spriteShape.StrokeLineJoin);
                Fb.Shape.AddStrokeMiterLimit(builder, spriteShape.StrokeMiterLimit);
                Fb.Shape.AddStrokeThickness(builder, spriteShape.StrokeThickness);
            }

            return Fb.Shape.EndShape(builder);
        }

        static Offset<Fb.Geometry> WriteGeometry(FlatBufferBuilder builder, GraphIndexer graph, CompositionGeometry value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.Geometry.StartGeometry(builder);
            AddBase(builder, compObj, Fb.Geometry.AddBase);
            Fb.Geometry.AddKind(builder, value switch
            {
                CompositionRectangleGeometry _ => Fb.GeometryKind.Rectangle,
                CompositionRoundedRectangleGeometry _ => Fb.GeometryKind.RoundedRectangle,
                CompositionEllipseGeometry _ => Fb.GeometryKind.Ellipse,
                _ => Fb.GeometryKind.Path,
            });

            Fb.Geometry.AddTrimStart(builder, value.TrimStart);
            Fb.Geometry.AddTrimEnd(builder, value.TrimEnd);
            Fb.Geometry.AddTrimOffset(builder, value.TrimOffset);

            switch (value)
            {
                case CompositionPathGeometry path:
                    Fb.Geometry.AddPath(
                        builder,
                        path.Path is null
                            ? Format.NullIndex
                            : graph.GetCanvasGeometry((CanvasGeometry)path.Path.Source));
                    break;
                case CompositionRectangleGeometry rectangle:
                    AddVec2(builder, rectangle.Offset, Fb.Geometry.AddOffset);
                    AddVec2(builder, rectangle.Size, Fb.Geometry.AddSize);
                    break;
                case CompositionRoundedRectangleGeometry roundedRectangle:
                    AddVec2(builder, roundedRectangle.Offset, Fb.Geometry.AddOffset);
                    AddVec2(builder, roundedRectangle.Size, Fb.Geometry.AddSize);
                    AddVec2(builder, roundedRectangle.CornerRadius, Fb.Geometry.AddCornerRadius);
                    break;
                case CompositionEllipseGeometry ellipse:
                    AddVec2(builder, ellipse.Center, Fb.Geometry.AddCenter);
                    AddVec2(builder, ellipse.Radius, Fb.Geometry.AddRadius);
                    break;
            }

            return Fb.Geometry.EndGeometry(builder);
        }

        static Offset<Fb.Brush> WriteBrush(FlatBufferBuilder builder, GraphIndexer graph, CompositionBrush value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            var gradientBrush = value as CompositionGradientBrush;

            var colorStops = gradientBrush is not null && gradientBrush.ColorStops.Count > 0
                ? Fb.Brush.CreateColorStopsVector(builder, gradientBrush.ColorStops.Select(graph.GetGradientStop).ToArray())
                : default;

            var sourceParameters = default(VectorOffset);
            if (value is CompositionEffectBrush effectBrush)
            {
                var sources = effectBrush.GetEffectFactory().Effect.Sources;
                var offsets = new Offset<Fb.SourceParameter>[sources.Count];
                for (var i = 0; i < sources.Count; i++)
                {
                    var name = sources[i].Name;
                    Fb.SourceParameter.StartSourceParameter(builder);
                    Fb.SourceParameter.AddName(builder, graph.GetString(name));
                    Fb.SourceParameter.AddBrush(builder, graph.GetBrush(effectBrush.GetSourceParameter(name)));
                    offsets[i] = Fb.SourceParameter.EndSourceParameter(builder);
                }

                if (offsets.Length > 0)
                {
                    sourceParameters = Fb.Brush.CreateSourceParametersVector(builder, offsets);
                }
            }

            Fb.Brush.StartBrush(builder);
            AddBase(builder, compObj, Fb.Brush.AddBase);
            Fb.Brush.AddKind(builder, value switch
            {
                CompositionLinearGradientBrush _ => Fb.BrushKind.LinearGradient,
                CompositionRadialGradientBrush _ => Fb.BrushKind.RadialGradient,
                CompositionSurfaceBrush _ => Fb.BrushKind.Surface,
                CompositionMaskBrush _ => Fb.BrushKind.Mask,
                CompositionEffectBrush _ => Fb.BrushKind.Effect,
                _ => Fb.BrushKind.Color,
            });

            switch (value)
            {
                case CompositionColorBrush colorBrush:
                    AddColor(builder, colorBrush.Color, Fb.Brush.AddColor);
                    break;
                case CompositionSurfaceBrush surfaceBrush:
                    Fb.Brush.AddSurface(builder, graph.GetSurface(surfaceBrush.Surface));
                    break;
                case CompositionMaskBrush maskBrush:
                    Fb.Brush.AddSource(builder, graph.GetBrush(maskBrush.Source));
                    Fb.Brush.AddMask(builder, graph.GetBrush(maskBrush.Mask));
                    break;
                case CompositionEffectBrush effect:
                    Fb.Brush.AddEffect(builder, graph.GetEffect(effect.GetEffectFactory().Effect));

                    if (sourceParameters.Value != 0)
                    {
                        Fb.Brush.AddSourceParameters(builder, sourceParameters);
                    }

                    break;
            }

            if (gradientBrush is not null)
            {
                AddVec2(builder, gradientBrush.AnchorPoint, Fb.Brush.AddAnchorPoint);
                AddVec2(builder, gradientBrush.CenterPoint, Fb.Brush.AddCenterPoint);

                if (colorStops.Value != 0)
                {
                    Fb.Brush.AddColorStops(builder, colorStops);
                }

                Fb.Brush.AddExtendMode(builder, (byte?)gradientBrush.ExtendMode);
                Fb.Brush.AddInterpolationSpace(builder, (byte?)gradientBrush.InterpolationSpace);
                Fb.Brush.AddMappingMode(builder, (byte?)gradientBrush.MappingMode);
                AddVec2(builder, gradientBrush.Offset, Fb.Brush.AddOffset);
                Fb.Brush.AddRotationAngleInDegrees(builder, gradientBrush.RotationAngleInDegrees);
                AddVec2(builder, gradientBrush.Scale, Fb.Brush.AddScale);
                AddMat3x2(builder, gradientBrush.TransformMatrix, Fb.Brush.AddTransformMatrix);

                switch (gradientBrush)
                {
                    case CompositionLinearGradientBrush linear:
                        AddVec2(builder, linear.StartPoint, Fb.Brush.AddStartPoint);
                        AddVec2(builder, linear.EndPoint, Fb.Brush.AddEndPoint);
                        break;
                    case CompositionRadialGradientBrush radial:
                        AddVec2(builder, radial.EllipseCenter, Fb.Brush.AddEllipseCenter);
                        AddVec2(builder, radial.EllipseRadius, Fb.Brush.AddEllipseRadius);
                        AddVec2(builder, radial.GradientOriginOffset, Fb.Brush.AddGradientOriginOffset);
                        break;
                }
            }

            return Fb.Brush.EndBrush(builder);
        }

        static Offset<Fb.GradientStop> WriteGradientStop(FlatBufferBuilder builder, GraphIndexer graph, CompositionColorGradientStop value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.GradientStop.StartGradientStop(builder);
            AddBase(builder, compObj, Fb.GradientStop.AddBase);
            AddColor(builder, value.Color, Fb.GradientStop.AddColor);
            Fb.GradientStop.AddOffset(builder, value.Offset);
            return Fb.GradientStop.EndGradientStop(builder);
        }

        static Offset<Fb.ViewBox> WriteViewBox(FlatBufferBuilder builder, GraphIndexer graph, CompositionViewBox value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.ViewBox.StartViewBox(builder);
            AddBase(builder, compObj, Fb.ViewBox.AddBase);
            AddVec2(builder, value.Size, Fb.ViewBox.AddSize);
            return Fb.ViewBox.EndViewBox(builder);
        }

        static Offset<Fb.Clip> WriteClip(FlatBufferBuilder builder, GraphIndexer graph, CompositionClip value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.Clip.StartClip(builder);
            AddBase(builder, compObj, Fb.Clip.AddBase);
            Fb.Clip.AddKind(builder, value is CompositionGeometricClip ? Fb.ClipKind.Geometric : Fb.ClipKind.Inset);
            AddVec2(builder, value.CenterPoint, Fb.Clip.AddCenterPoint);
            AddVec2(builder, value.Scale, Fb.Clip.AddScale);

            switch (value)
            {
                case InsetClip inset:
                    Fb.Clip.AddLeftInset(builder, inset.LeftInset);
                    Fb.Clip.AddRightInset(builder, inset.RightInset);
                    Fb.Clip.AddTopInset(builder, inset.TopInset);
                    Fb.Clip.AddBottomInset(builder, inset.BottomInset);
                    break;
                case CompositionGeometricClip geometric:
                    Fb.Clip.AddGeometry(builder, graph.GetGeometry(geometric.Geometry));
                    break;
            }

            return Fb.Clip.EndClip(builder);
        }

        static Offset<Fb.Shadow> WriteShadow(FlatBufferBuilder builder, GraphIndexer graph, CompositionShadow value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.Shadow.StartShadow(builder);
            AddBase(builder, compObj, Fb.Shadow.AddBase);
            Fb.Shadow.AddKind(builder, Fb.ShadowKind.Drop);

            if (value is DropShadow dropShadow)
            {
                Fb.Shadow.AddBlurRadius(builder, dropShadow.BlurRadius);
                AddColor(builder, dropShadow.Color, Fb.Shadow.AddColor);
                Fb.Shadow.AddMask(builder, graph.GetBrush(dropShadow.Mask));
                AddVec3(builder, dropShadow.Offset, Fb.Shadow.AddOffset);
                Fb.Shadow.AddOpacity(builder, dropShadow.Opacity);
                Fb.Shadow.AddSourcePolicy(builder, (byte?)dropShadow.SourcePolicy);
            }

            return Fb.Shadow.EndShadow(builder);
        }

        static Offset<Fb.Surface> WriteSurface(FlatBufferBuilder builder, GraphIndexer graph, ICompositionSurface value, StringOffset[] strings)
        {
            var compObj = value is CompositionObject compositionObject
                ? WriteCompObj(builder, graph, compositionObject)
                : default;

            var bytes = value is LoadedImageSurfaceFromStream fromStream
                ? Fb.Surface.CreateBytesVector(builder, fromStream.Bytes)
                : default;

            Fb.Surface.StartSurface(builder);
            AddBase(builder, compObj, Fb.Surface.AddBase);
            Fb.Surface.AddKind(builder, value switch
            {
                LoadedImageSurfaceFromUri _ => Fb.SurfaceKind.LoadedImageFromUri,
                LoadedImageSurfaceFromStream _ => Fb.SurfaceKind.LoadedImageFromStream,
                _ => Fb.SurfaceKind.VisualSurface,
            });

            switch (value)
            {
                case CompositionVisualSurface visualSurface:
                    Fb.Surface.AddSourceVisual(builder, graph.GetVisual(visualSurface.SourceVisual));
                    AddVec2(builder, visualSurface.SourceSize, Fb.Surface.AddSourceSize);
                    AddVec2(builder, visualSurface.SourceOffset, Fb.Surface.AddSourceOffset);
                    break;
                case LoadedImageSurfaceFromUri fromUri:
                    Fb.Surface.AddUri(builder, graph.GetString(fromUri.Uri.ToString()));
                    break;
                case LoadedImageSurfaceFromStream _:
                    Fb.Surface.AddBytes(builder, bytes);
                    break;
            }

            return Fb.Surface.EndSurface(builder);
        }

        static Offset<Fb.Effect> WriteEffect(FlatBufferBuilder builder, GraphIndexer graph, GraphicsEffectBase value)
        {
            var sources = Fb.Effect.CreateSourcesVector(
                builder,
                value.Sources.Select(s => graph.GetString(s.Name)).ToArray());

            Fb.Effect.StartEffect(builder);
            Fb.Effect.AddKind(builder, value switch
            {
                GaussianBlurEffect _ => Fb.EffectKind.GaussianBlur,
                _ => Fb.EffectKind.Composite,
            });

            Fb.Effect.AddSources(builder, sources);

            switch (value)
            {
                case CompositeEffect composite:
                    Fb.Effect.AddMode(builder, (byte)composite.Mode);
                    break;
                case GaussianBlurEffect blur:
                    Fb.Effect.AddBlurAmount(builder, blur.BlurAmount);
                    break;
            }

            return Fb.Effect.EndEffect(builder);
        }

        static Offset<Fb.Easing> WriteEasing(FlatBufferBuilder builder, GraphIndexer graph, CompositionEasingFunction value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.Easing.StartEasing(builder);
            AddBase(builder, compObj, Fb.Easing.AddBase);
            Fb.Easing.AddKind(builder, value switch
            {
                CubicBezierEasingFunction _ => Fb.EasingKind.CubicBezier,
                StepEasingFunction _ => Fb.EasingKind.Step,
                _ => Fb.EasingKind.Linear,
            });

            switch (value)
            {
                case CubicBezierEasingFunction cubicBezier:
                    AddVec2(builder, cubicBezier.ControlPoint1, Fb.Easing.AddControlPoint1);
                    AddVec2(builder, cubicBezier.ControlPoint2, Fb.Easing.AddControlPoint2);
                    break;
                case StepEasingFunction step:
                    Fb.Easing.AddStepCount(builder, step.StepCount);
                    Fb.Easing.AddInitialStep(builder, step.InitialStep);
                    Fb.Easing.AddFinalStep(builder, step.FinalStep);
                    Fb.Easing.AddIsInitialStepSingleFrame(builder, step.IsInitialStepSingleFrame);
                    Fb.Easing.AddIsFinalStepSingleFrame(builder, step.IsFinalStepSingleFrame);
                    break;
            }

            return Fb.Easing.EndEasing(builder);
        }

        static Offset<Fb.Animation> WriteAnimation(FlatBufferBuilder builder, GraphIndexer graph, CompositionAnimation value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            // ReferenceParameters is a dictionary, so it is sorted by name to keep the
            // output deterministic.
            var parameters = value.ReferenceParameters.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            var parameterOffsets = new Offset<Fb.ReferenceParameter>[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                Fb.ReferenceParameter.StartReferenceParameter(builder);
                Fb.ReferenceParameter.AddName(builder, graph.GetString(parameters[i].Key));
                Fb.ReferenceParameter.AddTarget(builder, graph.GetObjectReference(parameters[i].Value));
                parameterOffsets[i] = Fb.ReferenceParameter.EndReferenceParameter(builder);
            }

            var parametersVector = parameters.Length == 0
                ? default
                : Fb.Animation.CreateReferenceParametersVector(builder, parameterOffsets);

            var keyFramesVector = default(VectorOffset);
            if (value is KeyFrameAnimation_ keyFrameAnimation)
            {
                var keyFrames = keyFrameAnimation.KeyFrames.ToArray();
                var keyFrameOffsets = new Offset<Fb.KeyFrame>[keyFrames.Length];
                for (var i = 0; i < keyFrames.Length; i++)
                {
                    keyFrameOffsets[i] = WriteKeyFrame(builder, graph, keyFrames[i]);
                }

                if (keyFrameOffsets.Length > 0)
                {
                    keyFramesVector = Fb.Animation.CreateKeyFramesVector(builder, keyFrameOffsets);
                }
            }

            Fb.Animation.StartAnimation(builder);
            AddBase(builder, compObj, Fb.Animation.AddBase);
            Fb.Animation.AddKind(builder, value switch
            {
                ExpressionAnimation _ => Fb.AnimationKind.Expression,
                ColorKeyFrameAnimation _ => Fb.AnimationKind.Color,
                BooleanKeyFrameAnimation _ => Fb.AnimationKind.Boolean,
                PathKeyFrameAnimation _ => Fb.AnimationKind.Path,
                ScalarKeyFrameAnimation _ => Fb.AnimationKind.Scalar,
                Vector2KeyFrameAnimation _ => Fb.AnimationKind.Vector2,
                Vector3KeyFrameAnimation _ => Fb.AnimationKind.Vector3,
                Vector4KeyFrameAnimation _ => Fb.AnimationKind.Vector4,
                _ => throw new InvalidOperationException($"Unsupported animation {value.Type}."),
            });

            Fb.Animation.AddTarget(builder, graph.GetString(value.Target));

            if (parametersVector.Value != 0)
            {
                Fb.Animation.AddReferenceParameters(builder, parametersVector);
            }

            if (value is ExpressionAnimation expressionAnimation)
            {
                Fb.Animation.AddExpression(builder, graph.GetString(expressionAnimation.Expression.ToText()));
            }

            if (value is KeyFrameAnimation_ keyFrames_)
            {
                Fb.Animation.AddDurationTicks(builder, keyFrames_.Duration.Ticks);

                if (keyFramesVector.Value != 0)
                {
                    Fb.Animation.AddKeyFrames(builder, keyFramesVector);
                }
            }

            if (value is ColorKeyFrameAnimation color)
            {
                Fb.Animation.AddInterpolationColorSpace(builder, (byte)color.InterpolationColorSpace);
            }

            return Fb.Animation.EndAnimation(builder);
        }

        static Offset<Fb.KeyFrame> WriteKeyFrame(FlatBufferBuilder builder, GraphIndexer graph, KeyFrameAnimation_.KeyFrame value)
        {
            Fb.KeyFrame.StartKeyFrame(builder);
            Fb.KeyFrame.AddProgress(builder, value.Progress);
            Fb.KeyFrame.AddEasing(builder, graph.GetEasing(value.Easing));
            Fb.KeyFrame.AddKind(builder, value.Type == KeyFrameType.Expression ? Fb.KeyFrameKind.Expression : Fb.KeyFrameKind.Value);

            switch (value)
            {
                case KeyFrameAnimation_.ExpressionKeyFrame expression:
                    Fb.KeyFrame.AddExpression(builder, graph.GetString(expression.Expression.ToText()));
                    break;
                case KeyFrameAnimation<float, Expressions.Scalar>.ValueKeyFrame scalar:
                    Fb.KeyFrame.AddScalar(builder, scalar.Value);
                    break;
                case KeyFrameAnimation<bool, Expressions.Boolean>.ValueKeyFrame boolean:
                    Fb.KeyFrame.AddScalar(builder, boolean.Value ? 1f : 0f);
                    break;
                case KeyFrameAnimation<Vector2, Expressions.Vector2>.ValueKeyFrame vector2:
                    Fb.KeyFrame.AddVector(builder, Fb.Vec4.CreateVec4(builder, vector2.Value.X, vector2.Value.Y, 0, 0));
                    break;
                case KeyFrameAnimation<Vector3, Expressions.Vector3>.ValueKeyFrame vector3:
                    Fb.KeyFrame.AddVector(builder, Fb.Vec4.CreateVec4(builder, vector3.Value.X, vector3.Value.Y, vector3.Value.Z, 0));
                    break;
                case KeyFrameAnimation<Vector4, Expressions.Vector4>.ValueKeyFrame vector4:
                    Fb.KeyFrame.AddVector(builder, Fb.Vec4.CreateVec4(builder, vector4.Value.X, vector4.Value.Y, vector4.Value.Z, vector4.Value.W));
                    break;
                case KeyFrameAnimation<Wui.Color, Expressions.Color>.ValueKeyFrame colorFrame:
                    Fb.KeyFrame.AddColor(builder, CreateColor(builder, colorFrame.Value));
                    break;
                case KeyFrameAnimation<CompositionPath, Expressions.Void>.ValueKeyFrame path:
                    Fb.KeyFrame.AddPath(builder, graph.GetCanvasGeometry((CanvasGeometry)path.Value.Source));
                    break;
            }

            return Fb.KeyFrame.EndKeyFrame(builder);
        }

        static Offset<Fb.PropertySet> WritePropertySet(FlatBufferBuilder builder, GraphIndexer graph, CompositionPropertySet value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            // Names is a SortedDictionary, so enumeration order is already deterministic.
            var names = value.Names.ToArray();
            var valueOffsets = new Offset<Fb.PropertyValue>[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                valueOffsets[i] = WritePropertyValue(builder, graph, value, names[i].Key, names[i].Value);
            }

            var valuesVector = names.Length == 0
                ? default
                : Fb.PropertySet.CreateValuesVector(builder, valueOffsets);

            Fb.PropertySet.StartPropertySet(builder);
            AddBase(builder, compObj, Fb.PropertySet.AddBase);
            Fb.PropertySet.AddOwner(builder, graph.GetObjectReference(value.Owner));

            if (valuesVector.Value != 0)
            {
                Fb.PropertySet.AddValues(builder, valuesVector);
            }

            return Fb.PropertySet.EndPropertySet(builder);
        }

        static Offset<Fb.PropertyValue> WritePropertyValue(
            FlatBufferBuilder builder,
            GraphIndexer graph,
            CompositionPropertySet propertySet,
            string name,
            PropertySetValueType type)
        {
            Fb.PropertyValue.StartPropertyValue(builder);
            Fb.PropertyValue.AddName(builder, graph.GetString(name));
            Fb.PropertyValue.AddType(builder, (Fb.PropertyValueType)type);

            switch (type)
            {
                case PropertySetValueType.Color:
                    propertySet.TryGetColor(name, out var color);
                    Fb.PropertyValue.AddColor(builder, CreateColor(builder, color!.Value));
                    break;
                case PropertySetValueType.Scalar:
                    propertySet.TryGetScalar(name, out var scalar);
                    Fb.PropertyValue.AddScalar(builder, scalar!.Value);
                    break;
                case PropertySetValueType.Vector2:
                    propertySet.TryGetVector2(name, out var vector2);
                    Fb.PropertyValue.AddVector(builder, Fb.Vec4.CreateVec4(builder, vector2!.Value.X, vector2.Value.Y, 0, 0));
                    break;
                case PropertySetValueType.Vector3:
                    propertySet.TryGetVector3(name, out var vector3);
                    Fb.PropertyValue.AddVector(builder, Fb.Vec4.CreateVec4(builder, vector3!.Value.X, vector3.Value.Y, vector3.Value.Z, 0));
                    break;
                case PropertySetValueType.Vector4:
                    propertySet.TryGetVector4(name, out var vector4);
                    Fb.PropertyValue.AddVector(builder, Fb.Vec4.CreateVec4(builder, vector4!.Value.X, vector4.Value.Y, vector4.Value.Z, vector4.Value.W));
                    break;
            }

            return Fb.PropertyValue.EndPropertyValue(builder);
        }

        static Offset<Fb.Controller> WriteController(FlatBufferBuilder builder, GraphIndexer graph, AnimationController value)
        {
            var compObj = WriteCompObj(builder, graph, value);

            Fb.Controller.StartController(builder);
            AddBase(builder, compObj, Fb.Controller.AddBase);
            Fb.Controller.AddTargetObject(builder, graph.GetObjectReference(value.TargetObject));
            Fb.Controller.AddTargetProperty(builder, graph.GetString(value.TargetProperty));
            Fb.Controller.AddIsPaused(builder, value.IsPaused);
            Fb.Controller.AddIsCustom(builder, value.IsCustom);
            return Fb.Controller.EndController(builder);
        }

        static Offset<Fb.CanvasGeometry> WriteCanvasGeometry(FlatBufferBuilder builder, GraphIndexer graph, CanvasGeometry value)
        {
            var geometries = value is CanvasGeometry.Group group
                ? Fb.CanvasGeometry.CreateGeometriesVector(builder, group.Geometries.Select(graph.GetCanvasGeometry).ToArray())
                : default;

            var ops = default(VectorOffset);
            var operands = default(VectorOffset);
            if (value is CanvasGeometry.Path pathGeometry)
            {
                var (opBytes, operandFloats) = FlattenPath(pathGeometry);
                ops = Fb.CanvasGeometry.CreateOpsVector(builder, opBytes);
                operands = Fb.CanvasGeometry.CreateOperandsVector(builder, operandFloats);
            }

            Fb.CanvasGeometry.StartCanvasGeometry(builder);
            Fb.CanvasGeometry.AddKind(builder, value.Type switch
            {
                CanvasGeometry.GeometryType.Combination => Fb.CanvasGeometryKind.Combination,
                CanvasGeometry.GeometryType.Ellipse => Fb.CanvasGeometryKind.Ellipse,
                CanvasGeometry.GeometryType.Group => Fb.CanvasGeometryKind.Group,
                CanvasGeometry.GeometryType.RoundedRectangle => Fb.CanvasGeometryKind.RoundedRectangle,
                CanvasGeometry.GeometryType.TransformedGeometry => Fb.CanvasGeometryKind.TransformedGeometry,
                _ => Fb.CanvasGeometryKind.Path,
            });

            switch (value)
            {
                case CanvasGeometry.Combination combination:
                    Fb.CanvasGeometry.AddA(builder, graph.GetCanvasGeometry(combination.A));
                    Fb.CanvasGeometry.AddB(builder, graph.GetCanvasGeometry(combination.B));
                    Fb.CanvasGeometry.AddCombineMode(builder, (byte)combination.CombineMode);
                    AddMat3x2(builder, combination.Matrix, Fb.CanvasGeometry.AddMatrix);
                    break;
                case CanvasGeometry.TransformedGeometry transformed:
                    Fb.CanvasGeometry.AddSource(builder, graph.GetCanvasGeometry(transformed.SourceGeometry));
                    AddMat3x2(builder, transformed.TransformMatrix, Fb.CanvasGeometry.AddMatrix);
                    break;
                case CanvasGeometry.Group groupGeometry:
                    Fb.CanvasGeometry.AddGeometries(builder, geometries);
                    Fb.CanvasGeometry.AddFillRule(builder, (byte)groupGeometry.FilledRegionDetermination);
                    break;
                case CanvasGeometry.Path path:
                    Fb.CanvasGeometry.AddFillRule(builder, (byte)path.FilledRegionDetermination);
                    Fb.CanvasGeometry.AddOps(builder, ops);
                    Fb.CanvasGeometry.AddOperands(builder, operands);
                    break;
                case CanvasGeometry.Ellipse ellipse:
                    Fb.CanvasGeometry.AddX(builder, ellipse.X);
                    Fb.CanvasGeometry.AddY(builder, ellipse.Y);
                    Fb.CanvasGeometry.AddRadiusX(builder, ellipse.RadiusX);
                    Fb.CanvasGeometry.AddRadiusY(builder, ellipse.RadiusY);
                    break;
                case CanvasGeometry.RoundedRectangle roundedRectangle:
                    Fb.CanvasGeometry.AddX(builder, roundedRectangle.X);
                    Fb.CanvasGeometry.AddY(builder, roundedRectangle.Y);
                    Fb.CanvasGeometry.AddW(builder, roundedRectangle.W);
                    Fb.CanvasGeometry.AddH(builder, roundedRectangle.H);
                    Fb.CanvasGeometry.AddRadiusX(builder, roundedRectangle.RadiusX);
                    Fb.CanvasGeometry.AddRadiusY(builder, roundedRectangle.RadiusY);
                    break;
            }

            return Fb.CanvasGeometry.EndCanvasGeometry(builder);
        }

        // Flattens the command list of a path into an opcode stream and a parallel
        // operand stream. This avoids a table per command, which would dominate the
        // size of a typical animation.
        static (byte[] Ops, float[] Operands) FlattenPath(CanvasGeometry.Path value)
        {
            var ops = new List<byte>(value.Commands.Length);
            var operands = new List<float>(value.Commands.Length * 2);

            foreach (var command in value.Commands)
            {
                switch (command)
                {
                    case CanvasPathBuilder.Command.BeginFigure begin:
                        ops.Add((byte)Fb.PathOp.BeginFigure);
                        operands.Add(begin.StartPoint.X);
                        operands.Add(begin.StartPoint.Y);
                        break;
                    case CanvasPathBuilder.Command.EndFigure end:
                        ops.Add((byte)Fb.PathOp.EndFigure);
                        operands.Add((float)end.FigureLoop);
                        break;
                    case CanvasPathBuilder.Command.AddLine line:
                        ops.Add((byte)Fb.PathOp.AddLine);
                        operands.Add(line.EndPoint.X);
                        operands.Add(line.EndPoint.Y);
                        break;
                    case CanvasPathBuilder.Command.AddCubicBezier bezier:
                        ops.Add((byte)Fb.PathOp.AddCubicBezier);
                        operands.Add(bezier.ControlPoint1.X);
                        operands.Add(bezier.ControlPoint1.Y);
                        operands.Add(bezier.ControlPoint2.X);
                        operands.Add(bezier.ControlPoint2.Y);
                        operands.Add(bezier.EndPoint.X);
                        operands.Add(bezier.EndPoint.Y);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported path command {command.Type}.");
                }
            }

            return (ops.ToArray(), operands.ToArray());
        }

        static Offset<Fb.Metadata> WriteMetadata(
            FlatBufferBuilder builder,
            GraphIndexer graph,
            LottieCompositionMetadata? metadata,
            IReadOnlyList<PropertyBinding>? propertyBindings,
            float width,
            float height)
        {
            var bindings = propertyBindings ?? Array.Empty<PropertyBinding>();
            var bindingOffsets = new Offset<Fb.PropertyBinding>[bindings.Count];
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                Fb.PropertyBinding.StartPropertyBinding(builder);
                Fb.PropertyBinding.AddName(builder, graph.GetString(binding.BindingName));
                Fb.PropertyBinding.AddDisplayName(builder, graph.GetString(binding.DisplayName));
                Fb.PropertyBinding.AddActualType(builder, (Fb.PropertyValueType)binding.ActualType);
                Fb.PropertyBinding.AddExposedType(builder, (Fb.PropertyValueType)binding.ExposedType);

                switch (binding.DefaultValue)
                {
                    case float scalar:
                        Fb.PropertyBinding.AddDefaultScalar(builder, scalar);
                        break;
                    case Vector2 vector2:
                        Fb.PropertyBinding.AddDefaultVector(builder, Fb.Vec4.CreateVec4(builder, vector2.X, vector2.Y, 0, 0));
                        break;
                    case Vector3 vector3:
                        Fb.PropertyBinding.AddDefaultVector(builder, Fb.Vec4.CreateVec4(builder, vector3.X, vector3.Y, vector3.Z, 0));
                        break;
                    case Vector4 vector4:
                        Fb.PropertyBinding.AddDefaultVector(builder, Fb.Vec4.CreateVec4(builder, vector4.X, vector4.Y, vector4.Z, vector4.W));
                        break;
                    case Wui.Color color:
                        Fb.PropertyBinding.AddDefaultColor(builder, CreateColor(builder, color));
                        break;
                }

                bindingOffsets[i] = Fb.PropertyBinding.EndPropertyBinding(builder);
            }

            var markers = metadata?.Markers ?? (IReadOnlyList<LottieMetadata.Marker>)Array.Empty<LottieMetadata.Marker>();
            var markerOffsets = new Offset<Fb.Marker>[markers.Count];
            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                Fb.Marker.StartMarker(builder);
                Fb.Marker.AddName(builder, graph.GetString(marker.Name));
                Fb.Marker.AddProgress(builder, (float)marker.Frame.Progress);

                // Marker durations are stored as a proportion of the whole animation, to
                // match Frame.Progress, so that a player needs no frame rate arithmetic.
                var totalFrames = metadata!.Duration.Frames;
                Fb.Marker.AddDurationProgress(
                    builder,
                    totalFrames == 0 ? 0f : (float)(marker.Duration.Frames / totalFrames));
                markerOffsets[i] = Fb.Marker.EndMarker(builder);
            }

            var name = graph.GetString(metadata?.CompositionName);

            var bindingsVector = bindings.Count == 0
                ? default
                : Fb.Metadata.CreatePropertyBindingsVector(builder, bindingOffsets);

            var markersVector = markers.Count == 0
                ? default
                : Fb.Metadata.CreateMarkersVector(builder, markerOffsets);

            // The theming property set is the one that has no owner, i.e. the one that was
            // created by Compositor.CreatePropertySet rather than by a composition object.
            var themingPropertySet = Format.NullIndex;
            for (var i = 0; i < graph.PropertySetList.Count; i++)
            {
                if (graph.PropertySetList[i].Owner is null)
                {
                    themingPropertySet = (uint)i;
                    break;
                }
            }

            Fb.Metadata.StartMetadata(builder);
            Fb.Metadata.AddName(builder, name);
            Fb.Metadata.AddWidth(builder, width);
            Fb.Metadata.AddHeight(builder, height);
            Fb.Metadata.AddDurationTicks(builder, metadata is null ? 0L : metadata.Duration.Time.Ticks);
            Fb.Metadata.AddFramesPerSecond(builder, (float)(metadata?.Duration.FPS ?? 0));

            if (bindingsVector.Value != 0)
            {
                Fb.Metadata.AddPropertyBindings(builder, bindingsVector);
            }

            if (markersVector.Value != 0)
            {
                Fb.Metadata.AddMarkers(builder, markersVector);
            }

            Fb.Metadata.AddThemingPropertySet(builder, themingPropertySet);
            return Fb.Metadata.EndMetadata(builder);
        }

        // ---------------------------------------------------------------------
        // Value helpers. A struct field is written only when the source property has a
        // value, so that "never set" and "set to the default" stay distinguishable.
        // ---------------------------------------------------------------------
        static Offset<Fb.Color> CreateColor(FlatBufferBuilder builder, Wui.Color value)
            => Fb.Color.CreateColor(builder, value.A, value.R, value.G, value.B);

        static void AddColor(FlatBufferBuilder builder, Wui.Color? value, Action<FlatBufferBuilder, Offset<Fb.Color>> add)
        {
            if (value.HasValue)
            {
                add(builder, CreateColor(builder, value.Value));
            }
        }

        static void AddVec2(FlatBufferBuilder builder, Vector2? value, Action<FlatBufferBuilder, Offset<Fb.Vec2>> add)
        {
            if (value.HasValue)
            {
                add(builder, Fb.Vec2.CreateVec2(builder, value.Value.X, value.Value.Y));
            }
        }

        static void AddVec2(FlatBufferBuilder builder, Vector2 value, Action<FlatBufferBuilder, Offset<Fb.Vec2>> add)
        {
            // A non-nullable property always has a value, but writing a zero is the same
            // as leaving the field out, so the zero case is skipped to save space.
            if (value != Vector2.Zero)
            {
                add(builder, Fb.Vec2.CreateVec2(builder, value.X, value.Y));
            }
        }

        static void AddVec3(FlatBufferBuilder builder, Vector3? value, Action<FlatBufferBuilder, Offset<Fb.Vec3>> add)
        {
            if (value.HasValue)
            {
                add(builder, Fb.Vec3.CreateVec3(builder, value.Value.X, value.Value.Y, value.Value.Z));
            }
        }

        static void AddMat3x2(FlatBufferBuilder builder, Matrix3x2? value, Action<FlatBufferBuilder, Offset<Fb.Mat3x2>> add)
        {
            if (value.HasValue)
            {
                var m = value.Value;
                add(builder, Fb.Mat3x2.CreateMat3x2(builder, m.M11, m.M12, m.M21, m.M22, m.M31, m.M32));
            }
        }

        static void AddMat3x2(FlatBufferBuilder builder, Matrix3x2 value, Action<FlatBufferBuilder, Offset<Fb.Mat3x2>> add)
        {
            if (!value.IsIdentity)
            {
                add(builder, Fb.Mat3x2.CreateMat3x2(builder, value.M11, value.M12, value.M21, value.M22, value.M31, value.M32));
            }
        }

        static void AddMat4x4(FlatBufferBuilder builder, Matrix4x4? value, Action<FlatBufferBuilder, Offset<Fb.Mat4x4>> add)
        {
            if (value.HasValue)
            {
                var m = value.Value;
                add(builder, Fb.Mat4x4.CreateMat4x4(
                    builder,
                    m.M11, m.M12, m.M13, m.M14,
                    m.M21, m.M22, m.M23, m.M24,
                    m.M31, m.M32, m.M33, m.M34,
                    m.M41, m.M42, m.M43, m.M44));
            }
        }
    }
}
