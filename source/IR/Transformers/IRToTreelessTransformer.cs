// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    sealed class IRToTreelessTransformer
    {
        readonly LayerCollection _layers;
        readonly AssetCollection _assets;

        IRToTreelessTransformer(LayerCollection layers, AssetCollection assets)
        {
            _layers = layers;
            _assets = assets;
        }

        public static Rendering Transform(IRComposition source)
        {
            var instance = new IRToTreelessTransformer(source.Layers, source.Assets);

            var size = new SizeRenderingContext(new Vector2(source.Width, source.Height));

            // Convert from layers to Renderings
            var renderings =
                (from rendering in instance.Detreeify(source.Layers)
                 select rendering.WithContext(size + rendering.Context)).ToArray();

            // Unify the timebases.
            var unifiedTimeBaseRenderings = renderings.Select(Rendering.UnifyTimebase).ToArray();

            // Optimize the contexts.
            var optimizedRenderings = unifiedTimeBaseRenderings.Select(CreateRenderingOptimizer(ContextOptimizers.Optimize)).ToArray();

            VisibilityGrouping.CreateVisibilityGroups(optimizedRenderings);

            var result = new Rendering(
                                new ContainerRenderingContent(optimizedRenderings),
                                new MetadataRenderingContext(name: $"Lottie {source.Name}", source: source));

            return result;
        }

        /// <summary>
        /// Creates an optimizer that optimizers renderings by optimizing their contexts
        /// with the given optimizer.
        /// </summary>
        static Func<Rendering, Rendering> CreateRenderingOptimizer(ContextTransformer contextOptimizer)
            => r => OptimizeRendering(r, contextOptimizer);

        /// <summary>
        /// Optimizes a rendering by optimizing its contexts.
        /// </summary>
        static Rendering OptimizeRendering(Rendering input, ContextTransformer contextOptimizer)
            => new Rendering(OptimizeContent(input.Content, contextOptimizer), OptimizeContext(input.Context, contextOptimizer));

        /// <summary>
        /// Optimizes content by optimizing its contexts.
        /// </summary>
        static RenderingContent OptimizeContent(RenderingContent input, ContextTransformer contextOptimizer)
            => input is ContainerRenderingContent container
             ? new ContainerRenderingContent(container.Items.Select(item => OptimizeRendering(item, contextOptimizer)).ToArray())
             : input;

        /// <summary>
        /// Optimizes a context with the given optimizer.
        /// </summary>
        static RenderingContext OptimizeContext(RenderingContext input, ContextTransformer optimizer)
             => optimizer(input);

        IEnumerable<Rendering> Detreeify(LayerCollection layers)
        {
            foreach (var layer in layers.GetLayersBottomToTop())
            {
                switch (layer.Type)
                {
                    case Layer.LayerType.Null:
                        // Null layers only exist to hold transforms.
                        continue;

                    case Layer.LayerType.PreComp:
                        foreach (var childLayer in DetreeifyPreCompLayer((PreCompLayer)layer))
                        {
                            yield return childLayer;
                        }

                        break;

                    case Layer.LayerType.Image:
                        yield return DetreeifyImageLayer((ImageLayer)layer);
                        break;

                    case Layer.LayerType.Shape:
                        foreach (var item in DetreeifyShapeLayer((ShapeLayer)layer))
                        {
                            yield return item;
                        }

                        break;

                    case Layer.LayerType.Solid:
                        yield return DetreeifySolidLayer((SolidLayer)layer);
                        break;

                    case Layer.LayerType.Text:
                        yield return DetreeifyTextLayer((TextLayer)layer);
                        break;

                    default:
                        throw Unreachable;
                }
            }
        }

        RenderingContext GetRenderingContextForLayer(Layer layer)
            => RenderingContext.Compose(GetRenderingContextsForLayer(layer));

        IEnumerable<RenderingContext> GetRenderingContextsForLayer(Layer layer)
        {
            yield return new MetadataRenderingContext(name: $"{layer.Type} {layer.Name}", source: layer);

            if (layer.TimeStretch != 1)
            {
                yield return new TimeStretchRenderingContext(layer.TimeStretch);
            }

            yield return new VisibilityRenderingContext(
                                    layer.IsHidden
                                        ? Array.Empty<double>()
                                        : new double[] { layer.InPoint, layer.OutPoint });

            yield return OpacityRenderingContext.Create(layer.Transform.Opacity);
            yield return new BlendModeRenderingContext(layer.BlendMode);

            if (layer.Masks.Any())
            {
                yield return new MasksRenderingContext(layer.Masks);
            }

            if (layer.MatteType != MatteType.None)
            {
                yield return new MatteTypeRenderingContext(layer.MatteType);
            }

            foreach (var context in GetInheritableTransformRenderingContextsForLayer(layer))
            {
                yield return context;
            }

            foreach (var context in layer.Effects.Select(eff => new EffectRenderingContext(eff)))
            {
                yield return context;
            }

            // All layers have a StartTime, but it's only relevant on precomps.
            // It affects only the children.
            if (layer.Type == Layer.LayerType.PreComp)
            {
                yield return new TimeOffsetRenderingContext(layer.StartTime);
            }
        }

        IEnumerable<RenderingContext> GetInheritableTransformRenderingContextsForLayer(Layer layer)
        {
            // Layers can inherit transforms, however they do not inherit opacity.
            if (layer.Parent.HasValue)
            {
                var parentLayer = _layers.GetLayerById(layer.Parent.Value);
                if (parentLayer != null)
                {
                    foreach (var inheritedTransform in GetInheritableTransformRenderingContextsForLayer(parentLayer))
                    {
                        yield return inheritedTransform;
                    }
                }
            }

            // Position indicates where in the parent the anchor point should be drawn.
            yield return PositionRenderingContext.Create(layer.Transform.Position.WithoutZ());

            // Rotation and scale are around the anchor point.
            yield return AnchorRenderingContext.Create(layer.Transform.Anchor.WithoutZ());

            // The size determines the bounding box. Only PreComp layers have a size.
            if (layer is PreCompLayer precomp)
            {
                yield return new SizeRenderingContext(precomp.Size);
            }

            yield return RotationRenderingContext.Create(layer.Transform.Rotation);
            yield return ScaleRenderingContext.Create(layer.Transform.ScalePercent.WithoutZ());
        }

        RenderingContext GetTransformRenderingContexts(Transform transform)
            => RenderingContext.Compose(
                    AnchorRenderingContext.Create(transform.Anchor.WithoutZ()),
                    PositionRenderingContext.Create(transform.Position.WithoutZ()),
                    RotationRenderingContext.Create(transform.Rotation),
                    ScaleRenderingContext.Create(transform.ScalePercent.WithoutZ()),
                    OpacityRenderingContext.Create(transform.Opacity));

        Rendering DetreeifyImageLayer(ImageLayer layer)
        {
            var imageAsset = (ImageAsset?)_assets.GetAssetById(layer.RefId);

            // TODO: if the asset isn't found, report an issue and return null.
            return new Rendering(
                        new ImageRenderingContent(imageAsset!),
                        GetRenderingContextForLayer(layer));
        }

        IEnumerable<Rendering> DetreeifyPreCompLayer(PreCompLayer layer)
        {
            var asset = _assets.GetAssetById(layer.RefId) as LayerCollectionAsset;

            if (asset is null)
            {
                // TODO - report an issue.
                yield break;
            }

            var precompContext = GetRenderingContextForLayer(layer);

            foreach (var child in Detreeify(asset.Layers))
            {
                yield return child.WithContext(precompContext + child.Context);
            }
        }

        RenderingContext DetreeifyShapeFill(ShapeFill shapeFill)
        {
            switch (shapeFill.FillKind)
            {
                case ShapeFill.ShapeFillKind.SolidColor:
                    {
                        var fill = (SolidColorFill)shapeFill;
                        return new FillRenderingContext(
                                        new SolidColorBrush(
                                            fill.Color,
                                            fill.Opacity));
                    }

                case ShapeFill.ShapeFillKind.LinearGradient:
                    {
                        var fill = (LinearGradientFill)shapeFill;
                        return new FillRenderingContext(
                                        new LinearGradientBrush(
                                            fill.StartPoint,
                                            fill.EndPoint,
                                            fill.GradientStops,
                                            fill.Opacity));
                    }

                case ShapeFill.ShapeFillKind.RadialGradient:
                    {
                        var fill = (RadialGradientFill)shapeFill;
                        return new FillRenderingContext(
                                    new RadialGradientBrush(
                                        fill.StartPoint,
                                        fill.EndPoint,
                                        fill.GradientStops,
                                        fill.Opacity,
                                        fill.HighlightLength,
                                        fill.HighlightDegrees));
                    }

                default:
                    throw Unreachable;
            }
        }

        RenderingContext DetreeifyShapeStroke(ShapeStroke shapeStroke)
        {
            switch (shapeStroke.StrokeKind)
            {
                case ShapeStroke.ShapeStrokeKind.SolidColor:
                    {
                        var stroke = (SolidColorStroke)shapeStroke;
                        return new StrokeRenderingContext(
                                        new SolidColorBrush(
                                            stroke.Color,
                                            stroke.Opacity),
                                        stroke.StrokeWidth,
                                        stroke.CapType,
                                        stroke.JoinType,
                                        stroke.MiterLimit);
                    }

                case ShapeStroke.ShapeStrokeKind.LinearGradient:
                    {
                        var stroke = (LinearGradientStroke)shapeStroke;
                        return new StrokeRenderingContext(
                                        new LinearGradientBrush(
                                            stroke.StartPoint,
                                            stroke.EndPoint,
                                            stroke.GradientStops,
                                            stroke.Opacity),
                                        stroke.StrokeWidth,
                                        stroke.CapType,
                                        stroke.JoinType,
                                        stroke.MiterLimit);
                    }

                case ShapeStroke.ShapeStrokeKind.RadialGradient:
                    {
                        var stroke = (RadialGradientStroke)shapeStroke;
                        return new StrokeRenderingContext(
                                    new RadialGradientBrush(
                                        stroke.StartPoint,
                                        stroke.EndPoint,
                                        stroke.GradientStops,
                                        stroke.Opacity,
                                        stroke.HighlightLength,
                                        stroke.HighlightDegrees),
                                    stroke.StrokeWidth,
                                    stroke.CapType,
                                    stroke.JoinType,
                                    stroke.MiterLimit);
                    }

                default:
                    throw Unreachable;
            }
        }

        Rendering DetreeifyShape(Shape shape)
        {
            RenderingContent content;
            RenderingContext context = RenderingContext.Null;

            switch (shape.ShapeType)
            {
                case ShapeType.Ellipse:
                    var ellipse = (Ellipse)shape;
                    content = new EllipseRenderingContent(ellipse.Diameter);
                    context = PositionRenderingContext.Create(ellipse.Position);
                    break;

                case ShapeType.Path:
                    var path = (Path)shape;

                    // Normalize the path data so that it has values close to 0. This
                    // makes debugging easier by removing large offsets from the path,
                    // and it makes it easier to canonicalize paths that are equivalent
                    // apart from their offset.
                    var minXY = path.Data.IsAnimated
                        ? path.Data.KeyFrames.Min(kf => kf.Value.GetMinimumXandY())
                        : path.Data.InitialValue.GetMinimumXandY();

                    context = new PositionRenderingContext.Static(minXY);
                    content = PathRenderingContent.Create(path.WithOffset(-minXY).Data);

                    break;

                case ShapeType.Polystar:
                    content = new UnsupportedRenderingContent("Polystar");
                    break;

                case ShapeType.Rectangle:
                    var rectangle = (Rectangle)shape;
                    content = new RectangleRenderingContent(rectangle.Size, rectangle.Roundness);
                    context = PositionRenderingContext.Create(rectangle.Position);
                    break;

                default:
                    throw Unreachable;
            }

            return new Rendering(content, context);
        }

        IEnumerable<Rendering> DetreeifyShapeLayerContents(IReadOnlyList<ShapeLayerContent> shapeLayerContents)
        {
            var context = RenderingContext.Null;

            // The contents are stored in reverse order, i.e. the context for the content
            // is after the content.
            foreach (var shapeLayerContent in shapeLayerContents.Reverse())
            {
                context = RenderingContext.Compose(
                    context,
                    new MetadataRenderingContext(name: shapeLayerContent.Name, source: shapeLayerContent),
                    new BlendModeRenderingContext(shapeLayerContent.BlendMode));

                switch (shapeLayerContent.ContentType)
                {
                    case ShapeContentType.Ellipse:
                    case ShapeContentType.Path:
                    case ShapeContentType.Polystar:
                    case ShapeContentType.Rectangle:
                        var shape = (Shape)shapeLayerContent;
                        if (shape.DrawingDirection == DrawingDirection.Reverse)
                        {
                            context += new DrawingDirectionRenderingContext(true);
                        }

                        var detreeifiedShape = DetreeifyShape((Shape)shapeLayerContent);
                        yield return detreeifiedShape.WithContext(context + detreeifiedShape.Context);
                        context = RenderingContext.Null;
                        break;

                    case ShapeContentType.Group:
                        {
                            var group = (ShapeGroup)shapeLayerContent;
                            var groupContents = DetreeifyShapeLayerContents(group.Contents).ToArray();
                            switch (groupContents.Length)
                            {
                                case 0:
                                    // Empty group.
                                    break;

                                case 1:
                                    // There's only one item in the group. The group is redundant.
                                    yield return groupContents[0].WithContext(context + groupContents[0].Context);
                                    break;

                                default:
                                    foreach (var item in groupContents)
                                    {
                                        yield return item.WithContext(context + item.Context);
                                    }

                                    // TODO: if there's group opacity, we will have to return this grouped.
                                    //yield return new Rendering(new GroupRenderingContent(groupContents), context);
                                    break;
                            }

                            context = RenderingContext.Null;
                        }

                        break;
                    case ShapeContentType.LinearGradientFill:
                    case ShapeContentType.SolidColorFill:
                    case ShapeContentType.RadialGradientFill:
                        context += DetreeifyShapeFill((ShapeFill)shapeLayerContent);
                        break;

                    case ShapeContentType.LinearGradientStroke:
                    case ShapeContentType.SolidColorStroke:
                    case ShapeContentType.RadialGradientStroke:
                        context += DetreeifyShapeStroke((ShapeStroke)shapeLayerContent);
                        break;

                    case ShapeContentType.MergePaths:
                    case ShapeContentType.Repeater:
                    case ShapeContentType.RoundCorners:
                    case ShapeContentType.TrimPath:
                        context += new UnsupportedRenderingContext(shapeLayerContent.ToString()!);
                        break;

                    case ShapeContentType.Transform:
                        context += GetTransformRenderingContexts((Transform)shapeLayerContent);
                        break;

                    default:
                        throw Unreachable;
                }
            }
        }

        static bool IsAlwaysOpaque(RenderingContext context)
        {
            foreach (var subContext in context.OfType<OpacityRenderingContext>())
            {
                switch (subContext)
                {
                    case OpacityRenderingContext.Static statc:
                        if (!statc.Opacity.IsOpaque)
                        {
                            return false;
                        }

                        break;
                    case OpacityRenderingContext.Animated animated:
                        if (!animated.Opacity.IsAlways(Opacity.Opaque))
                        {
                            return false;
                        }

                        break;
                    default: throw Unreachable;
                }
            }

            return true;
        }

        IEnumerable<Rendering> DetreeifyShapeLayer(ShapeLayer layer)
        {
            var contents = DetreeifyShapeLayerContents(layer.Contents).ToArray();
            var context = GetRenderingContextForLayer(layer);

            // TODO - if the context is not opaque, we need to group the content.
            foreach (var item in contents)
            {
                yield return item.WithContext(context + item.Context);
            }
        }

        Rendering DetreeifySolidLayer(SolidLayer layer)
        {
            return new Rendering(
                        new SolidRenderingContent(
                            width: layer.Width,
                            height: layer.Height,
                            color: layer.Color),
                        GetRenderingContextForLayer(layer));
        }

        Rendering DetreeifyTextLayer(TextLayer layer)
        {
            return new Rendering(
                        new UnsupportedRenderingContent("Text"),
                        GetRenderingContextForLayer(layer));
        }
    }
}
