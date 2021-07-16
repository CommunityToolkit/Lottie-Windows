// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Layer;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.ShapeLayerContent;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
#if PUBLIC_LottieData
    public
#endif

    sealed class LottieCompositionOptimizer
    {
        private LottieComposition OriginalComposition { get; }

        private List<Asset> NewAssets { get; }

        private Asset? GetAssetById(string id)
        {
            return OriginalComposition.Assets.GetAssetById(id) ?? NewAssets.Find((Asset a) => a.Id == id);
        }

        public LottieCompositionOptimizer(LottieComposition composition)
        {
            OriginalComposition = composition;
            NewAssets = new List<Asset>();
        }

        public LottieComposition GetOptimized()
        {
            List<Layer> layers = OriginalComposition.Layers.GetLayersBottomToTop().ToList();
            layers.Reverse();

            // todo: make this more obvious
            for (double minimalScore = 0.95, maximalAllowedDistance = 10.0; minimalScore > 0.1; minimalScore *= 0.8, maximalAllowedDistance *= 1.5)
            {
                for (double allowedDistance = 1.0; allowedDistance < maximalAllowedDistance; allowedDistance *= 2.0)
                {
                    for (int i = 0; i < layers.Count; i++)
                    {
                        for (int j = 0; j < layers.Count; j++)
                        {
                            if (i == j || layers[j].InPoint < layers[i].OutPoint || layers[j].InPoint - layers[i].OutPoint > allowedDistance)
                            {
                                continue;
                            }

                            var mergeRes = MergeLayers(layers[i], layers[j]);

                            if (mergeRes.Success && minimalScore <= mergeRes.Score)
                            {
                                if (i > j)
                                {
                                    layers.RemoveAt(i);
                                    layers.RemoveAt(j);
                                }
                                else
                                {
                                    layers.RemoveAt(j);
                                    layers.RemoveAt(i);
                                }

                                layers.Add(mergeRes.Value!);
                                i--;
                                break;
                            }
                        }
                    }
                }
            }

            List<Asset> usedAssets = new List<Asset>();

            usedAssets.AddRange(OriginalComposition.Assets);
            usedAssets.AddRange(NewAssets);

            return new LottieComposition(
                OriginalComposition.Name,
                OriginalComposition.Width,
                OriginalComposition.Height,
                OriginalComposition.InPoint,
                OriginalComposition.OutPoint,
                OriginalComposition.FramesPerSecond,
                OriginalComposition.Is3d,
                OriginalComposition.Version,
                new AssetCollection(usedAssets),
                OriginalComposition.Chars,
                OriginalComposition.Fonts,
                new LayerCollection(layers),
                OriginalComposition.Markers,
                OriginalComposition.ExtraData);
        }

        Animatable<T> ShiftAnimatable<T>(Animatable<T> a, double shift)
            where T : IEquatable<T>
        {
            if (!a.IsAnimated)
            {
                return new Animatable<T>(a.InitialValue);
            }

            var keyFrames = new List<KeyFrame<T>>();

            foreach (var kf in a.KeyFrames)
            {
                keyFrames.Add(new KeyFrame<T>(kf.Frame + shift, kf.Value, kf.SpatialBezier, kf.Easing));
            }

            return new Animatable<T>(keyFrames);
        }

        IAnimatableVector3 ShiftIAnimatableVector3(IAnimatableVector3 a, double shift)
        {
            if (a is AnimatableVector3)
            {
                var v = ShiftAnimatable((AnimatableVector3)a, shift);
                return v.IsAnimated ? new AnimatableVector3(v.KeyFrames) : new AnimatableVector3(v.InitialValue);
            }

            Debug.Assert(a is AnimatableXYZ, "There are only AnimatableXYZ and AnimatableVector3 implementations");

            var aXYZ = (AnimatableXYZ)a;
            var resX = ShiftAnimatable(aXYZ.X, shift);
            var resY = ShiftAnimatable(aXYZ.Y, shift);
            var resZ = ShiftAnimatable(aXYZ.Z, shift);

            return new AnimatableXYZ(resX, resY, resZ);
        }

        Transform ShiftTransform(Transform a, double shift)
        {
            return new Transform(
                new ShapeLayerContentArgs { Name = a.Name, MatchName = a.MatchName, BlendMode = a.BlendMode },
                ShiftIAnimatableVector3(a.Anchor, shift),
                ShiftIAnimatableVector3(a.Position, shift),
                ShiftIAnimatableVector3(a.ScalePercent, shift),
                ShiftAnimatable(a.Rotation, shift),
                ShiftAnimatable(a.Opacity, shift)
                );
        }

        ShapeLayer ShiftShapeLayer(ShapeLayer a, double shift)
        {
            var args = CopyArgs(a);

            args.Transform = ShiftTransform(a.Transform, shift);
            args.StartFrame += shift;
            args.InFrame += shift;
            args.OutFrame += shift;

            // TODO: Offset Contents!
            return new ShapeLayer(args, a.Contents);
        }

        NullLayer ShiftNullLayer(NullLayer a, double shift)
        {
            var args = CopyArgs(a);

            args.Transform = ShiftTransform(a.Transform, shift);
            args.StartFrame += shift;
            args.InFrame += shift;
            args.OutFrame += shift;

            return new NullLayer(args);
        }

        Result<Layer> ShiftLayer(Layer a, double shift)
        {
            switch (a.Type)
            {
                case LayerType.Shape:
                    return new Result<Layer>(ShiftShapeLayer((ShapeLayer)a, shift));
                case LayerType.Null:
                    return new Result<Layer>(ShiftNullLayer((NullLayer)a, shift));

                    // TODO: Implement for other types.
            }

            return Result<Layer>.Failed;
        }

        Result<LayerCollection> ShiftLayerCollection(LayerCollection a, double shift)
        {
            var layers = a.GetLayersBottomToTop();

            var layersAfterShift = new List<Layer>();

            foreach (var layer in layers)
            {
                var layerShiftRes = ShiftLayer(layer, shift);

                if (!layerShiftRes.Success)
                {
                    return Result<LayerCollection>.Failed;
                }

                layersAfterShift.Add(layerShiftRes.Value!);
            }

            return new Result<LayerCollection>(new LayerCollection(layersAfterShift));
        }

        Result<LayerCollectionAsset> ShiftLayerCollectionAsset(LayerCollectionAsset a, double shift)
        {
            var layerCollectionRes = ShiftLayerCollection(a.Layers, shift);

            if (!layerCollectionRes.Success)
            {
                return Result<LayerCollectionAsset>.Failed;
            }

            return new Result<LayerCollectionAsset>(new LayerCollectionAsset(a.Id, layerCollectionRes.Value!));
        }

        Result<Asset> ShiftAsset(Asset a, double shift)
        {
            switch (a.Type)
            {
                case Asset.AssetType.LayerCollection:
                    return Result<Asset>.From(ShiftLayerCollectionAsset((LayerCollectionAsset)a, shift));

                    // TODO: Implement for other types.
            }

            return Result<Asset>.Failed;
        }

        Result<Transform> MergeTransform(Transform a, Range aRange, Transform b, Range bRange, bool strict = false)
        {
            if (a.BlendMode != b.BlendMode)
            {
                return Result<Transform>.Failed;
            }

            var anchor = MergeIAnimatableVector3(a.Anchor, aRange, b.Anchor, bRange);
            var position = MergeIAnimatableVector3(a.Position, aRange, b.Position, bRange);
            var scalePercent = MergeIAnimatableVector3(a.ScalePercent, aRange, b.ScalePercent, bRange);
            var rotation = MergeAnimatable(a.Rotation, aRange, b.Rotation, bRange);
            var opacity = strict ?
                MergeAnimatableOpacityStrict(a.Opacity, aRange, b.Opacity, bRange) :
                MergeAnimatable(a.Opacity, aRange, b.Opacity, bRange);

            if (!anchor.Success || !position.Success || !scalePercent.Success || !rotation.Success || !opacity.Success)
            {
                return Result<Transform>.Failed;
            }

            ShapeLayerContentArgs args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                BlendMode = a.BlendMode,
                MatchName = $"{a.MatchName}{b.MatchName}",
            };

            return new Result<Transform>(new Transform(args, anchor.Value!, position.Value!, scalePercent.Value!, rotation.Value!, opacity.Value!));
        }

        Result<IAnimatableVector3> MergeIAnimatableVector3(IAnimatableVector3 a, Range aRange, IAnimatableVector3 b, Range bRange)
        {
            if (a is AnimatableVector3 && b is AnimatableVector3)
            {
                var res = MergeAnimatable((AnimatableVector3)a, aRange, (AnimatableVector3)b, bRange);
                if (!res.Success)
                {
                    return Result<IAnimatableVector3>.Failed;
                }

                AnimatableVector3 resVector3;
                if (res.Value!.IsAnimated)
                {
                    resVector3 = new AnimatableVector3(res.Value.KeyFrames);
                }
                else
                {
                    resVector3 = new AnimatableVector3(res.Value.InitialValue);
                }

                return new Result<IAnimatableVector3>(resVector3);
            }
            else if (a is AnimatableXYZ && b is AnimatableXYZ)
            {
                var aXYZ = (AnimatableXYZ)a;
                var bXYZ = (AnimatableXYZ)b;
                var resX = MergeAnimatable(aXYZ.X, aRange, bXYZ.X, bRange);
                var resY = MergeAnimatable(aXYZ.Y, aRange, bXYZ.Y, bRange);
                var resZ = MergeAnimatable(aXYZ.Z, aRange, bXYZ.Z, bRange);
                if (resX.Success && resY.Success && resZ.Success)
                {
                    return new Result<IAnimatableVector3>(new AnimatableXYZ(resX.Value!, resY.Value!, resZ.Value!));
                }
            }

            return Result<IAnimatableVector3>.Failed;
        }

        Result<IAnimatableVector2> MergeIAnimatableVector2(IAnimatableVector2 a, Range aRange, IAnimatableVector2 b, Range bRange)
        {
            if (a is AnimatableVector2 && b is AnimatableVector2)
            {
                var res = MergeAnimatable((AnimatableVector2)a, aRange, (AnimatableVector2)b, bRange);
                if (!res.Success)
                {
                    return Result<IAnimatableVector2>.Failed;
                }

                AnimatableVector2 resVector2;
                if (res.Value!.IsAnimated)
                {
                    resVector2 = new AnimatableVector2(res.Value.KeyFrames);
                }
                else
                {
                    resVector2 = new AnimatableVector2(res.Value.InitialValue);
                }

                return new Result<IAnimatableVector2>(resVector2);
            }
            else if (a is AnimatableXY && b is AnimatableXY)
            {
                var aXY = (AnimatableXY)a;
                var bXY = (AnimatableXY)b;
                var resX = MergeAnimatable(aXY.X, aRange, bXY.X, bRange);
                var resY = MergeAnimatable(aXY.Y, aRange, bXY.Y, bRange);
                if (resX.Success && resY.Success)
                {
                    return new Result<IAnimatableVector2>(new AnimatableXY(resX.Value!, resY.Value!));
                }
            }

            return Result<IAnimatableVector2>.Failed;
        }

        Result<Animatable<T>> MergeAnimatable<T>(Animatable<T> a, Range aRange, Animatable<T> b, Range bRange)
            where T : IEquatable<T>
        {
            if (!a.IsAnimated && !b.IsAnimated)
            {
                if (a.InitialValue.Equals(b.InitialValue))
                {
                    return new Result<Animatable<T>>(new Animatable<T>(a.InitialValue));
                }

                // We do not want to introduce more animated values so just return Fail instead of trying
                // make two key frames with a.InitialValue and b.InitialValue.
                return Result<Animatable<T>>.Failed;
            }

            /* TODO: Optional case
            if (a.IsAnimated != b.IsAnimated)
            {
                return Result<Animatable<T>>.Failed;
            }*/

            List<KeyFrame<T>> mergedKeyFrames = new List<KeyFrame<T>>();

            if (a.IsAnimated)
            {
                foreach (var kf in a.KeyFrames)
                {
                    mergedKeyFrames.Add(new KeyFrame<T>(kf.Frame, kf.Value, kf.SpatialBezier, kf.Easing));
                }
            }
            else
            {
                mergedKeyFrames.Add(new KeyFrame<T>(aRange.InPoint, a.InitialValue, HoldEasing.Instance));
            }

            mergedKeyFrames.Add(new KeyFrame<T>(bRange.InPoint, b.InitialValue, HoldEasing.Instance));

            if (b.IsAnimated)
            {
                foreach (var kf in b.KeyFrames)
                {
                    mergedKeyFrames.Add(new KeyFrame<T>(kf.Frame, kf.Value, kf.SpatialBezier, kf.Easing));
                }
            }

            // Ensure that keyframes are in correct order!
            for (int i = 0; i + 1 < mergedKeyFrames.Count; i++)
            {
                if (mergedKeyFrames[i].Frame > mergedKeyFrames[i + 1].Frame)
                {
                    return Result<Animatable<T>>.Failed;
                }
            }

            return new Result<Animatable<T>>(new Animatable<T>(mergedKeyFrames));
        }

        Result<Animatable<Opacity>> MergeAnimatableOpacityStrict(Animatable<Opacity> a, Range aRange, Animatable<Opacity> b, Range bRange)
        {
            Debug.Assert(!aRange.Intersect(bRange), "Ranges should not intersect");

            if (!a.IsAnimated && !b.IsAnimated)
            {
                if (aRange.OutPoint == bRange.InPoint)
                {
                    if (a.InitialValue.Equals(b.InitialValue))
                    {
                        return new Result<Animatable<Opacity>>(new Animatable<Opacity>(a.InitialValue));
                    }
                    else
                    {
                        var keyFrames = new List<KeyFrame<Opacity>>();
                        keyFrames.Add(new KeyFrame<Opacity>(aRange.InPoint, a.InitialValue, HoldEasing.Instance));
                        keyFrames.Add(new KeyFrame<Opacity>(bRange.InPoint, b.InitialValue, HoldEasing.Instance));
                        return new Result<Animatable<Opacity>>(new Animatable<Opacity>(keyFrames));
                    }
                }
                else
                {
                    var keyFrames = new List<KeyFrame<Opacity>>();
                    keyFrames.Add(new KeyFrame<Opacity>(aRange.InPoint, a.InitialValue, HoldEasing.Instance));
                    keyFrames.Add(new KeyFrame<Opacity>(aRange.OutPoint, Opacity.Transparent, HoldEasing.Instance));
                    keyFrames.Add(new KeyFrame<Opacity>(bRange.InPoint, b.InitialValue, HoldEasing.Instance));
                    return new Result<Animatable<Opacity>>(new Animatable<Opacity>(keyFrames));
                }
            }

            List<KeyFrame<Opacity>> mergedKeyFrames = new List<KeyFrame<Opacity>>();

            if (a.IsAnimated)
            {
                foreach (var kf in a.KeyFrames)
                {
                    mergedKeyFrames.Add(new KeyFrame<Opacity>(kf.Frame, kf.Value, kf.SpatialBezier, kf.Easing));
                }
            }
            else
            {
                mergedKeyFrames.Add(new KeyFrame<Opacity>(aRange.InPoint, a.InitialValue, HoldEasing.Instance));
            }

            if (aRange.OutPoint < bRange.InPoint)
            {
                mergedKeyFrames.Add(new KeyFrame<Opacity>(aRange.OutPoint, Opacity.Transparent, HoldEasing.Instance));
            }

            if (b.IsAnimated)
            {
                if (b.KeyFrames[0].Frame != bRange.InPoint)
                {
                    mergedKeyFrames.Add(new KeyFrame<Opacity>(bRange.InPoint, b.InitialValue, HoldEasing.Instance));
                }

                foreach (var kf in b.KeyFrames)
                {
                    mergedKeyFrames.Add(new KeyFrame<Opacity>(kf.Frame, kf.Value, kf.SpatialBezier, kf.Easing));
                }
            }
            else
            {
                mergedKeyFrames.Add(new KeyFrame<Opacity>(bRange.InPoint, b.InitialValue, HoldEasing.Instance));
            }

            // Ensure that keyframes are in correct order!
            for (int i = 0; i + 1 < mergedKeyFrames.Count; i++)
            {
                if (mergedKeyFrames[i].Frame > mergedKeyFrames[i + 1].Frame)
                {
                    return Result<Animatable<Opacity>>.Failed;
                }
            }

            return new Result<Animatable<Opacity>>(new Animatable<Opacity>(mergedKeyFrames));
        }

        Result<ShapeGroup> MergeShapeGroup(ShapeGroup a, Range aRange, ShapeGroup b, Range bRange)
        {
            if (a.BlendMode != b.BlendMode)
            {
                return Result<ShapeGroup>.Failed;
            }

            List<ShapeLayerContent> contents = new List<ShapeLayerContent>();

            for (int i = 0; i < a.Contents.Count; i++)
            {
                var res = MergeShapeLayerContents(a.Contents[i], aRange, b.Contents[i], bRange);
                if (!res.Success)
                {
                    return Result<ShapeGroup>.Failed;
                }

                contents.Add(res.Value!);
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            return new Result<ShapeGroup>(new ShapeGroup(args, contents));
        }

        Result<Path> MergePaths(Path a, Range aRange, Path b, Range bRange)
        {
            if (a.BlendMode != b.BlendMode || a.DrawingDirection != b.DrawingDirection)
            {
                return Result<Path>.Failed;
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            var geometryData = MergeAnimatable(a.Data, aRange, b.Data, bRange);

            if (!geometryData.Success)
            {
                return Result<Path>.Failed;
            }

            return new Result<Path>(new Path(args, a.DrawingDirection, geometryData.Value!));
        }

        Result<Ellipse> MergeEllipses(Ellipse a, Range aRange, Ellipse b, Range bRange)
        {
            if (a.BlendMode != b.BlendMode || a.DrawingDirection != b.DrawingDirection)
            {
                return Result<Ellipse>.Failed;
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            var positionMergeRes = MergeIAnimatableVector3(a.Position, aRange, b.Position, bRange);
            var diameterMergeRes = MergeIAnimatableVector3(a.Diameter, aRange, b.Diameter, bRange);

            if (!positionMergeRes.Success || !diameterMergeRes.Success)
            {
                return Result<Ellipse>.Failed;
            }

            return new Result<Ellipse>(new Ellipse(args, a.DrawingDirection, positionMergeRes.Value!, diameterMergeRes.Value!));
        }

        Result<LinearGradientFill> MergeLinearGradientFills(LinearGradientFill a, Range aRange, LinearGradientFill b, Range bRange)
        {
            if (a.BlendMode != b.BlendMode || a.FillType != b.FillType)
            {
                return Result<LinearGradientFill>.Failed;
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            var opacity = MergeAnimatable(a.Opacity, aRange, b.Opacity, bRange);
            var startPoint = MergeIAnimatableVector2(a.StartPoint, aRange, b.StartPoint, bRange);
            var endPoint = MergeIAnimatableVector2(a.EndPoint, aRange, b.EndPoint, bRange);
            var gradientStops = MergeAnimatable(a.GradientStops, aRange, b.GradientStops, bRange);

            if (!opacity.Success || !startPoint.Success || !endPoint.Success || !gradientStops.Success)
            {
                return Result<LinearGradientFill>.Failed;
            }

            return new Result<LinearGradientFill>(new LinearGradientFill(args, a.FillType, opacity.Value!, startPoint.Value!, endPoint.Value!, gradientStops.Value!));
        }

        Result<SolidColorFill> MergeSolidColorFills(SolidColorFill a, Range aRange, SolidColorFill b, Range bRange)
        {
            if (a.BlendMode != b.BlendMode || a.FillType != b.FillType)
            {
                return Result<SolidColorFill>.Failed;
            }

            var name = $"{a.Name} {b.Name}";

            var matchName = $"{a.MatchName} {b.MatchName}";

            var args = new ShapeLayerContentArgs
            {
                Name = name,
                MatchName = matchName,
                BlendMode = a.BlendMode,
            };

            var opacity = MergeAnimatable(a.Opacity, aRange, b.Opacity, bRange);
            var color = MergeAnimatable(a.Color, aRange, b.Color, bRange);

            if (!opacity.Success || !color.Success)
            {
                return Result<SolidColorFill>.Failed;
            }

            return new Result<SolidColorFill>(new SolidColorFill(args, a.FillType, opacity.Value!, color.Value!));
        }

        Result<ShapeLayerContent> MergeShapeLayerContents(ShapeLayerContent a, Range aRange, ShapeLayerContent b, Range bRange)
        {
            if (a.ContentType != b.ContentType)
            {
                return Result<ShapeLayerContent>.Failed;
            }

            switch (a.ContentType)
            {
                case ShapeContentType.Group:
                    return Result<ShapeLayerContent>.From(MergeShapeGroup((ShapeGroup)a, aRange, (ShapeGroup)b, bRange));
                case ShapeContentType.Path:
                    return Result<ShapeLayerContent>.From(MergePaths((Path)a, aRange, (Path)b, bRange));
                case ShapeContentType.Ellipse:
                    return Result<ShapeLayerContent>.From(MergeEllipses((Ellipse)a, aRange, (Ellipse)b, bRange));
                case ShapeContentType.LinearGradientFill:
                    return Result<ShapeLayerContent>.From(MergeLinearGradientFills((LinearGradientFill)a, aRange, (LinearGradientFill)b, bRange));
                case ShapeContentType.Transform:
                    return Result<ShapeLayerContent>.From(MergeTransform((Transform)a, aRange, (Transform)b, bRange));
                case ShapeContentType.SolidColorFill:
                    return Result<ShapeLayerContent>.From(MergeSolidColorFills((SolidColorFill)a, aRange, (SolidColorFill)b, bRange));
            }

            return Result<ShapeLayerContent>.Failed;
        }

        bool MakeUberOptimizerPass(List<LayerGroup> layers)
        {
            LayersGraph graph = new LayersGraph(layers);
            graph.MergeStep((Layer a, Layer b, bool ignoreParent) => MergeLayers(a, b, ignoreParent));
            var next = graph.GetListWithMergedLayersInOrder();

            if (next.Count != layers.Count)
            {
                layers.Clear();
                layers.AddRange(next);
                return true;
            }

            return false;
        }

        LayerArgs CopyArgs(Layer layer)
        {
            return new LayerArgs
            {
                Name = layer.Name,
                Index = layer.Index,
                Parent = layer.Parent,
                IsHidden = layer.IsHidden,
                Transform = layer.Transform,
                TimeStretch = layer.TimeStretch,
                StartFrame = layer.InPoint,
                InFrame = layer.InPoint,
                OutFrame = layer.OutPoint,
                BlendMode = layer.BlendMode,
                Is3d = layer.Is3d,
                AutoOrient = layer.AutoOrient,
                LayerMatteType = layer.LayerMatteType,
                Effects = layer.Effects,
                Masks = layer.Masks,
            };
        }

        LayerArgs CopyArgsAndClamp(Layer layer, Range range)
        {
            var args = CopyArgs(layer);

            args.InFrame = Math.Clamp(args.InFrame, range.InPoint, range.OutPoint - 1e-6);
            args.OutFrame = Math.Clamp(args.OutFrame, range.InPoint + 1e-6, range.OutPoint);

            return args;
        }

        Layer ClampLayer(Layer layer, Range range)
        {
            switch (layer.Type)
            {
                case LayerType.Shape:
                    {
                        return new ShapeLayer(CopyArgsAndClamp(layer, range), ((ShapeLayer)layer).Contents);
                    }

                case LayerType.Null:
                    {
                        return new NullLayer(CopyArgsAndClamp(layer, range));
                    }

                case LayerType.PreComp:
                    {
                        var preCompLayer = (PreCompLayer)layer;
                        return new PreCompLayer(CopyArgsAndClamp(layer, range), preCompLayer.RefId, preCompLayer.Width, preCompLayer.Height);
                    }

                case LayerType.Image:
                    {
                        return new ImageLayer(CopyArgsAndClamp(layer, range), ((ImageLayer)layer).RefId);
                    }

                case LayerType.Solid:
                    {
                        var solidLayer = (SolidLayer)layer;
                        return new SolidLayer(CopyArgsAndClamp(layer, range), solidLayer.Width, solidLayer.Height, solidLayer.Color);
                    }

                case LayerType.Text:
                    {
                        return new TextLayer(CopyArgsAndClamp(layer, range), ((TextLayer)layer).RefId);
                    }
            }

            Debug.Assert(false, "Should not happen");

            return layer;
        }

        Result<LayerCollection> MergeLayerCollections(LayerCollection a, Range aParentRange, LayerCollection b, Range bParentRange)
        {
            var aLayers = a.GetLayersBottomToTop().Select(layer => ClampLayer(layer, aParentRange)).ToList();
            var bLayers = b.GetLayersBottomToTop().Select(layer => ClampLayer(layer, bParentRange)).ToList();

            // We need layers in Top-To-Bottom order here.
            aLayers.Reverse();
            bLayers.Reverse();

            // Here we are assigning new indices while preserving relative order.
            var aMapping = new LayersIndexMapper();
            var bMapping = new LayersIndexMapper();

            var generator = new LayersIndexMapper.IndexGenerator();

            foreach (var layer in aLayers)
            {
                aMapping.SetMapping(layer.Index, generator.GenerateIndex());
            }

            foreach (var layer in bLayers)
            {
                bMapping.SetMapping(layer.Index, generator.GenerateIndex());
            }

            foreach (var layer in aLayers)
            {
                aMapping.RemapLayer(layer);
            }

            foreach (var layer in bLayers)
            {
                bMapping.RemapLayer(layer);
            }

            var aLayerGroups = LayerGroup.LayersToLayerGroups(aLayers, (Layer mainLayer, Layer? matteLayer) => {
                return mainLayer.OutPoint < aParentRange.OutPoint;
            });

            var bLayerGroups = LayerGroup.LayersToLayerGroups(bLayers, (Layer mainLayer, Layer? matteLayer) => {
                return mainLayer.InPoint > bParentRange.InPoint;
            });

            var layerGroups = new List<LayerGroup>();
            layerGroups.AddRange(aLayerGroups);
            layerGroups.AddRange(bLayerGroups);

            // Optimize while we can.
            while (MakeUberOptimizerPass(layerGroups))
            {
                // Keep optimizing!
            }

            // Score for merging two layer collections is the number of merged layers
            // divided by number of layers in smaller collection.
            // todo: compute layers instead of layer groups, there is a bug with search animation (??)
            double intersectionOverMinimumScore =
                (aLayerGroups.Count + bLayerGroups.Count - layerGroups.Count) * 1.0 / Math.Min(aLayerGroups.Count, bLayerGroups.Count);

            var layers = LayerGroup.LayerGroupsToLayers(layerGroups);

            // Layers are stored in Bottom-To-Top order.
            layers.Reverse();

            return new Result<LayerCollection>(new LayerCollection(layers), intersectionOverMinimumScore);
        }

        Result<LayerCollectionAsset> MergeLayerCollectionAssets(LayerCollectionAsset a, Range aParentRange, LayerCollectionAsset b, Range bParentRange)
        {
            var layerCollection = MergeLayerCollections(a.Layers, aParentRange, b.Layers, bParentRange);

            if (!layerCollection.Success)
            {
                return Result<LayerCollectionAsset>.Failed;
            }

            return new Result<LayerCollectionAsset>(new LayerCollectionAsset($"{a.Id} {b.Id}", layerCollection.Value!), layerCollection.Score);
        }

        /// <summary>
        /// Merge two assets that are not intersecting into one layer.
        /// </summary>
        /// <param name="a">First asset.</param>
        /// <param name="aParentRange">Visible time range for the first layer. Everything that is outside of this range will be invisible.</param>
        /// <param name="b">Second asset.</param>
        /// <param name="bParentRange">Visible time range for the second layer. Everything that is outside of this range will be invisible.</param>
        /// <returns>Result of merging two assets.</returns>
        Result<Asset> MergeAssets(Asset a, Range aParentRange, Asset b, Range bParentRange)
        {
            if (a.Type != b.Type)
            {
                return Result<Asset>.Failed;
            }

            switch (a.Type)
            {
                case Asset.AssetType.LayerCollection:
                    return Result<Asset>.From(MergeLayerCollectionAssets((LayerCollectionAsset)a, aParentRange, (LayerCollectionAsset)b, bParentRange));
            }

            return Result<Asset>.Failed;
        }

        /// <summary>
        /// Merge two layers which time ranges are not intersecting into one layer.
        /// </summary>
        /// <param name="a">First layer to merge.</param>
        /// <param name="b">Second layer to merge.</param>
        /// <param name="ignoreParent">Pass true if you want to ignore Parent equal check, use if you are sure that parent will be the same after the merge.</param>
        /// <returns>Result of merging two layers.</returns>
        private Result<Layer> MergeLayers(Layer a, Layer b, bool ignoreParent = false)
        {
            // Layer a must go be before layer b
            if (a.InPoint > b.InPoint)
            {
                return MergeLayers(b, a, ignoreParent);
            }

            if (a.Type != b.Type ||
                a.AutoOrient != b.AutoOrient ||
                a.BlendMode != b.BlendMode ||
                a.Effects.Count > 0 || b.Effects.Count > 0 || // TODO: not tested for layers with effects
                a.Is3d != b.Is3d ||
                a.IsHidden != b.IsHidden ||
                a.LayerMatteType != b.LayerMatteType ||
                a.Masks.Count > 0 || b.Masks.Count > 0 || // TODO: not tested for layers with masks
                (!ignoreParent && a.Parent != b.Parent) ||
                a.TimeStretch != 1.0 || b.TimeStretch != 1.0 || // TODO: not tested for layers with time stretch != 1
                a.OutPoint > b.InPoint || // check if they are intersecting
                a.StartTime > a.InPoint ||
                b.StartTime > b.InPoint)
            {
                return Result<Layer>.Failed;
            }

            switch (a.Type)
            {
                case LayerType.PreComp:
                    return Result<Layer>.From(MergePreCompLayers((PreCompLayer)a, (PreCompLayer)b));
                case LayerType.Shape:
                    return Result<Layer>.From(MergeShapeLayers((ShapeLayer)a, (ShapeLayer)b));
                case LayerType.Null:
                    return Result<Layer>.From(MergeNullLayers((NullLayer)a, (NullLayer)b));
            }

            return Result<Layer>.Failed;
        }

        Result<ShapeLayer> MergeShapeLayers(ShapeLayer a, ShapeLayer b)
        {
            if (a.Contents.Count != b.Contents.Count)
            {
                return Result<ShapeLayer>.Failed;
            }

            var transformMergeRes = MergeTransform(a.Transform, Range.ForLayer(a), b.Transform, Range.ForLayer(b));

            if (!transformMergeRes.Success)
            {
                return Result<ShapeLayer>.Failed;
            }

            var args = CopyArgs(a);

            args.Name = $"{a.Name} {b.Name}";
            args.OutFrame = b.OutPoint;
            args.Transform = transformMergeRes.Value!;

            double totalScore = transformMergeRes.Score;

            List<ShapeLayerContent> contents = new List<ShapeLayerContent>();

            for (int i = 0; i < a.Contents.Count; i++)
            {
                var res = MergeShapeLayerContents(a.Contents[i], Range.ForLayer(a), b.Contents[i], Range.ForLayer(b));

                if (!res.Success)
                {
                    return Result<ShapeLayer>.Failed;
                }

                contents.Add(res.Value!);
                totalScore += res.Score;
            }

            return new Result<ShapeLayer>(new ShapeLayer(args, contents), totalScore);
        }

        Result<NullLayer> MergeNullLayers(NullLayer a, NullLayer b)
        {
            var transformMergeRes = MergeTransform(a.Transform, Range.ForLayer(a), b.Transform, Range.ForLayer(b));

            if (!transformMergeRes.Success)
            {
                return Result<NullLayer>.Failed;
            }

            var args = CopyArgs(a);

            args.Name = $"{a.Name} {b.Name}";
            args.OutFrame = b.OutPoint;
            args.Transform = transformMergeRes.Value!;

            return new Result<NullLayer>(new NullLayer(args), transformMergeRes.Score);
        }

        Result<PreCompLayer> MergePreCompLayers(PreCompLayer a, PreCompLayer b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return Result<PreCompLayer>.Failed;
            }

            var aAsset = GetAssetById(a.RefId);

            var bAsset = GetAssetById(b.RefId);

            if (aAsset == null || bAsset == null || aAsset == bAsset)
            {
                return Result<PreCompLayer>.Failed;
            }

            double shift = b.InPoint - a.InPoint;

            var aAssetShiftRes = ShiftAsset(aAsset, 0.0);
            var bAssetShiftRes = ShiftAsset(bAsset, shift);

            if (!bAssetShiftRes.Success || !aAssetShiftRes.Success)
            {
                return Result<PreCompLayer>.Failed;
            }

            var asset = MergeAssets(
                aAssetShiftRes.Value!,
                Range.ForLayer(a).ShiftLeft(a.InPoint),
                bAssetShiftRes.Value!,
                Range.ForLayer(b).ShiftLeft(a.InPoint)
                );

            if (!asset.Success)
            {
                return Result<PreCompLayer>.Failed;
            }

            NewAssets.Add(asset.Value!);

            var transformMergeRes = MergeTransform(a.Transform, Range.ForLayer(a), b.Transform, Range.ForLayer(b), true);

            if (!transformMergeRes.Success)
            {
                return Result<PreCompLayer>.Failed;
            }

            var args = CopyArgs(a);

            args.Name = $"{a.Name} {b.Name}";
            args.OutFrame = b.OutPoint;
            args.Transform = transformMergeRes.Value!;

            return new Result<PreCompLayer>(
                new PreCompLayer(args, asset.Value!.Id, a.Width, a.Height),
                asset.Score
                );
        }
    }
}