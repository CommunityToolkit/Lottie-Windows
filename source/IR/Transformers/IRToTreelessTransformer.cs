// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless.RenderingContexts;

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

        public static TreelessComposition Transform(IRComposition source)
        {
            var instance = new IRToTreelessTransformer(source.Layers, source.Assets);
            var clip = new ClipRenderingContext { Width = source.Width, Height = source.Height };
            var detreeifiedLayers =
                (from contentAndContext in instance.Detreeify(source.Layers)
                 select contentAndContext.WithContext(clip + contentAndContext.Context)).ToArray();
            return new TreelessComposition(source, detreeifiedLayers);
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
            yield return new MetadataRenderingContext(name: layer.Name);

            if (layer.TimeStretch != 1)
            {
                yield return new TimeStretchRenderingContext(layer.TimeStretch);
            }

            yield return new TimeOffsetRenderingContext(layer.StartTime);
            yield return new VisibilityRenderingContext { InPoint = layer.InPoint, OutPoint = layer.OutPoint, IsHidden = layer.IsHidden };
            yield return new OpacityRenderingContext { Opacity = layer.Transform.Opacity };
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

        ContentAndContext DetreeifyShapeLayer(ShapeLayer layer)
        {
            return new ContentAndContext(
                    new ShapeRenderingContent(layer.Contents),
                    GetRenderingContextForLayer(layer));
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
