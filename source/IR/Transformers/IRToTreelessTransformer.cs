// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;

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

            var clip = new ClipRenderingContext(Vector2.Zero, new Vector2(source.Width, source.Height));

            // Convert from layers to Renderings
            var renderings =
                (from contentAndContext in instance.Detreeify(source.Layers)
                 select contentAndContext.WithContext(clip + contentAndContext.Context)).ToArray();

            // Optimize the contexts.
            var optimizedRenderings = renderings.Select(Optimize).ToArray();

            // Unify the timebases.
            optimizedRenderings = optimizedRenderings.Select(item => Rendering.UnifyTimebase(item, 0)).ToArray();

            var result = new Rendering(
                                new GroupRenderingContent(optimizedRenderings),
                                new MetadataRenderingContext($"Lottie {source.Name}") { Source = source });

            return result;
        }

        static RenderingContext ElideMetadata(RenderingContext input)
            => input.Filter((MetadataRenderingContext c) => false);

        static Rendering Optimize(Rendering input)
            => new Rendering(Optimize(input.Content), Optimize(input.Context));

        static RenderingContent Optimize(RenderingContent input)
            => input is GroupRenderingContent group
            ? new GroupRenderingContent(group.Items.Select(item => Optimize(item)).ToArray())
             : input;

        static RenderingContext Optimize(RenderingContext input)
        {
            var result = input;

            // Remove the metadata. For now we don't need it and it's easier to
            // see what's going on without it.
            result = ElideMetadata(result);
            result = AnchorRenderingContext.WithoutRedundants(result);
            result = BlendModeRenderingContext.WithoutRedundants(result);
            result = OpacityRenderingContext.WithoutRedundants(result);
            result = PositionRenderingContext.WithoutRedundants(result);
            result = RotationRenderingContext.WithoutRedundants(result);
            result = ScaleRenderingContext.WithoutRedundants(result);
            result = VisibilityRenderingContext.WithoutRedundants(result);
            result = ClipRenderingContext.WithoutRedundants(result);
            return result;
        }

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
                        yield return DetreeifyShapeLayer((ShapeLayer)layer);
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
            => RenderingContext.Compose(GetRenderingContextsForLayer(layer).ToArray());

        IEnumerable<RenderingContext> GetRenderingContextsForLayer(Layer layer)
        {
            yield return new MetadataRenderingContext(name: $"{layer.Type} {layer.Name}") { Source = layer };

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

            yield return AnchorRenderingContext.Create(layer.Transform.Anchor);
            yield return PositionRenderingContext.Create(layer.Transform.Position);
            yield return RotationRenderingContext.Create(layer.Transform.Rotation);
            yield return ScaleRenderingContext.Create(layer.Transform.ScalePercent);
        }

        RenderingContext GetTransformRenderingContexts(Transform transform)
            => RenderingContext.Compose(
                    AnchorRenderingContext.Create(transform.Anchor),
                    PositionRenderingContext.Create(transform.Position),
                    RotationRenderingContext.Create(transform.Rotation),
                    ScaleRenderingContext.Create(transform.ScalePercent),
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

            var clipContext = new ClipRenderingContext(Vector2.Zero, new Vector2(layer.Width, layer.Height));

            foreach (var child in Detreeify(asset.Layers))
            {
                yield return child.WithContext(clipContext + GetRenderingContextForLayer(layer) + child.Context);
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
                    content = PathRenderingContent.Create(path.Data);
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
                    new MetadataRenderingContext(shapeLayerContent.Name) { Source = shapeLayerContent },
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
                                    yield return new Rendering(new GroupRenderingContent(groupContents), context);
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

        Rendering DetreeifyShapeLayer(ShapeLayer layer)
        {
            var contents = DetreeifyShapeLayerContents(layer.Contents).ToArray();
            var context = GetRenderingContextForLayer(layer);

            switch (contents.Length)
            {
                case 0:
                    return new Rendering(RenderingContent.Null, RenderingContext.Null);
                case 1:
                    return contents[0].WithContext(context + contents[0].Context);
                default:
                    return new Rendering(new GroupRenderingContent(contents), context);
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

        static Exception Unreachable => new InvalidOperationException();
    }
}
