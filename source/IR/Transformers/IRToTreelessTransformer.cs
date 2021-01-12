// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeless;

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
            // TODO: insert a clip into the list of RenderingContext in each layer. The clip comes
            //       from the IRComposition's size.
            var instance = new IRToTreelessTransformer(source.Layers, source.Assets);
            var clip = new ClipRenderingContext { Width = source.Width, Height = source.Height };
            var detreeifiedLayers =
                (from contextAndLayer in instance.Detreeify(source.Layers)
                 select (context: clip + contextAndLayer.context, contextAndLayer.layer)).ToArray();
            return new TreelessComposition(source, detreeifiedLayers);
        }

        IEnumerable<(RenderingContext context, TreelessLayer layer)> Detreeify(LayerCollection layers)
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
            foreach (var context in GetTransformRenderingContextsForLayer(layer))
            {
                yield return context;
            }

            foreach (var context in layer.Effects.Select(eff => new EffectRenderingContext(eff)))
            {
                yield return context;
            }
        }

        IEnumerable<RenderingContext> GetTransformRenderingContextsForLayer(Layer layer)
        {
            if (layer.Parent.HasValue)
            {
                var parentLayer = _layers.GetLayerById(layer.Parent.Value);
                if (parentLayer != null)
                {
                    foreach (var inheritedTransform in GetTransformRenderingContextsForLayer(parentLayer))
                    {
                        yield return inheritedTransform;
                    }
                }
            }

            yield return new AnchorRenderingContext { Anchor = layer.Transform.Anchor };
            yield return new PositionRenderingContext { Position = layer.Transform.Position };
            yield return new RotationRenderingContext { Rotation = layer.Transform.Rotation };
            yield return new ScaleRenderingContext { ScalePercent = layer.Transform.ScalePercent };
        }

        (RenderingContext, TreelessLayer) DetreeifyImageLayer(ImageLayer layer)
        {
            var imageAsset = (ImageAsset?)_assets.GetAssetById(layer.RefId);

            // TODO: if the asset isn't found, report an issue and return null.
            return (GetRenderingContextForLayer(layer), new TreelessImageLayer(
                blendMode: layer.BlendMode,
                is3d: layer.Is3d,
                matteType: layer.MatteType,
                masks: layer.Masks,
                imageAsset!));
        }

        IEnumerable<(RenderingContext, TreelessLayer)> DetreeifyPreCompLayer(PreCompLayer layer)
        {
            var asset = _assets.GetAssetById(layer.RefId) as LayerCollectionAsset;

            if (asset is null)
            {
                // TODO - report an issue.
                yield break;
            }

            var clipContext = new ClipRenderingContext { Width = layer.Width, Height = layer.Height };

            foreach (var (context, childLayer) in Detreeify(asset.Layers))
            {
                yield return (clipContext + GetRenderingContextForLayer(layer) + context, childLayer);
            }
        }

        (RenderingContext, TreelessLayer) DetreeifyShapeLayer(ShapeLayer layer)
        {
            return (GetRenderingContextForLayer(layer), new TreelessShapeLayer(
                blendMode: layer.BlendMode,
                is3d: layer.Is3d,
                matteType: layer.MatteType,
                masks: layer.Masks,
                layer.Contents));
        }

        (RenderingContext, TreelessLayer) DetreeifySolidLayer(SolidLayer layer)
        {
            return (GetRenderingContextForLayer(layer),
                    new TreelessSolidLayer(
                        blendMode: layer.BlendMode,
                        is3d: layer.Is3d,
                        matteType: layer.MatteType,
                        masks: layer.Masks,
                        width: layer.Width,
                        height: layer.Height,
                        color: layer.Color));
        }

        (RenderingContext, TreelessLayer) DetreeifyTextLayer(TextLayer layer)
        {
            return (GetRenderingContextForLayer(layer), new TreelessTextLayer(
                                blendMode: layer.BlendMode,
                                is3d: layer.Is3d,
                                matteType: layer.MatteType,
                                masks: layer.Masks));
        }

        static Exception Unreachable => new InvalidOperationException();
    }
}
