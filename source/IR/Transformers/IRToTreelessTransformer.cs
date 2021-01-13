// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Optimization;
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

        public static ContentAndContext Transform(IRComposition source)
        {
            var instance = new IRToTreelessTransformer(source.Layers, source.Assets);

            var clip = new ClipRenderingContext(source.Width, source.Height);

            var detreeifiedLayers =
                (from contentAndContext in instance.Detreeify(source.Layers)
                 select contentAndContext.WithContext(clip + contentAndContext.Context)).ToArray();

            var optimizedDetreeifiedLayers =
                (from contentAndContext in detreeifiedLayers
                 select new ContentAndContext(
                     contentAndContext.Content,
                     RenderingContextOptimizer.Optimize(contentAndContext.Context))
                 ).ToArray();

            var result = new ContentAndContext(
                                new GroupRenderingContent(optimizedDetreeifiedLayers),
                                new MetadataRenderingContext("Lottie") { Source = source });

            return result;
        }

        IEnumerable<ContentAndContext> Detreeify(LayerCollection layers)
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
            yield return new MetadataRenderingContext(name: layer.Name) { Source = layer };

            if (layer.TimeStretch != 1)
            {
                yield return new TimeStretchRenderingContext(layer.TimeStretch);
            }

            yield return new TimeOffsetRenderingContext(layer.StartTime);
            yield return new VisibilityRenderingContext { InPoint = layer.InPoint, OutPoint = layer.OutPoint, IsHidden = layer.IsHidden };
            yield return new OpacityRenderingContext(layer.Transform.Opacity);
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

            yield return new AnchorRenderingContext(layer.Transform.Anchor);
            yield return new PositionRenderingContext(layer.Transform.Position);
            yield return new RotationRenderingContext(layer.Transform.Rotation);
            yield return new ScaleRenderingContext(layer.Transform.ScalePercent);
        }

        RenderingContext GetTransformRenderingContexts(Transform transform)
            => RenderingContext.Compose(
                    new AnchorRenderingContext(transform.Anchor),
                    new PositionRenderingContext(transform.Position),
                    new RotationRenderingContext(transform.Rotation),
                    new ScaleRenderingContext(transform.ScalePercent),
                    new OpacityRenderingContext(transform.Opacity));

        ContentAndContext DetreeifyImageLayer(ImageLayer layer)
        {
            var imageAsset = (ImageAsset?)_assets.GetAssetById(layer.RefId);

            // TODO: if the asset isn't found, report an issue and return null.
            return new ContentAndContext(
                        new ImageRenderingContent(imageAsset!),
                        GetRenderingContextForLayer(layer));
        }

        IEnumerable<ContentAndContext> DetreeifyPreCompLayer(PreCompLayer layer)
        {
            var asset = _assets.GetAssetById(layer.RefId) as LayerCollectionAsset;

            if (asset is null)
            {
                // TODO - report an issue.
                yield break;
            }

            var clipContext = new ClipRenderingContext(layer.Width, layer.Height);

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

        ContentAndContext DetreeifyShape(Shape shape)
        {
            RenderingContent content;
            RenderingContext context = NullRenderingContext.Instance;

            switch (shape.ShapeType)
            {
                case ShapeType.Ellipse:
                    var ellipse = (Ellipse)shape;
                    content = new EllipseRenderingContent(ellipse.Diameter);
                    context = new PositionRenderingContext(ellipse.Position);
                    break;

                case ShapeType.Path:
                    var path = (Path)shape;
                    content = new PathRenderingContent(path.Data);
                    break;

                case ShapeType.Polystar:
                    content = new UnsupportedRenderingContent("Polystar");
                    break;

                case ShapeType.Rectangle:
                    var rectangle = (Rectangle)shape;
                    content = new RectangleRenderingContent(rectangle.Size, rectangle.Roundness);
                    context = new PositionRenderingContext(rectangle.Position);
                    break;

                default:
                    throw Unreachable;
            }

            return new ContentAndContext(content, context);
        }

        IEnumerable<ContentAndContext> DetreeifyShapeLayerContents(IReadOnlyList<ShapeLayerContent> shapeLayerContents)
        {
            RenderingContext context = NullRenderingContext.Instance;

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
                        context = NullRenderingContext.Instance;
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
                                    yield return new ContentAndContext(new GroupRenderingContent(groupContents), context);
                                    break;
                            }

                            context = NullRenderingContext.Instance;
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

        ContentAndContext DetreeifyShapeLayer(ShapeLayer layer)
        {
            var contents = DetreeifyShapeLayerContents(layer.Contents).ToArray();
            var context = GetRenderingContextForLayer(layer);

            switch (contents.Length)
            {
                case 0:
                    return new ContentAndContext(NullRenderingContent.Instance, NullRenderingContext.Instance);
                case 1:
                    return contents[0].WithContext(context + contents[0].Context);
                default:
                    return new ContentAndContext(new GroupRenderingContent(contents), context);
            }
        }

        ContentAndContext DetreeifySolidLayer(SolidLayer layer)
        {
            return new ContentAndContext(
                        new SolidRenderingContent(
                            width: layer.Width,
                            height: layer.Height,
                            color: layer.Color),
                        GetRenderingContextForLayer(layer));
        }

        ContentAndContext DetreeifyTextLayer(TextLayer layer)
        {
            return new ContentAndContext(
                        new UnsupportedRenderingContent("Text"),
                        GetRenderingContextForLayer(layer));
        }

        static Exception Unreachable => new InvalidOperationException();
    }
}
