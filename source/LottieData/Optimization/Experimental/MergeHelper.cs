// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Layer;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.ShapeLayerContent;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// This class provides methods to merge some lottie data structures together if they are similar enough.
    /// While merging some layers it can produce new <see cref="Asset"/>s,
    /// all generated assets are stored in <see cref="AssetsGenerated"/> field.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif

    class MergeHelper
    {
        public List<Asset> AssetsGenerated { get; } = new List<Asset>();

        private List<Asset> AssetsOriginal { get; } = new List<Asset>();

        public MergeHelper(LottieComposition composition)
        {
            AssetsOriginal.AddRange(composition.Assets);
        }

        private Asset? GetAssetById(string id)
        {
            return AssetsOriginal.Find((Asset a) => a.Id == id) ?? AssetsGenerated.Find((Asset a) => a.Id == id);
        }

        /// <summary>
        /// Optimizer pass. Builds a graph of layer groups and merge all layer groups that still can be merged and
        /// will not affect z-order of other layer groups. Returns true if anything was actually optimized.
        /// You can call it several times until it starts returning false.
        /// </summary>
        bool MakeOptimizerPass(List<LayerGroup> layerGroups)
        {
            LayersGraph graph = new LayersGraph(layerGroups);
            graph.MergeAllPossibleLayerGroups(this);
            var layerGroupsOptimized = graph.GetLayerGroups();

            if (layerGroupsOptimized.Count != layerGroups.Count)
            {
                layerGroups.Clear();
                layerGroups.AddRange(layerGroupsOptimized);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Merge two transform components together.
        /// </summary>
        /// <param name="a">First transform.</param>
        /// <param name="aRange">Visible range for the first transform.</param>
        /// <param name="b"> Second transform.</param>
        /// <param name="bRange">Visible range for the second transform.</param>
        /// <param name="strict">If you use strict=true then it is guaranteed that opacity will be set to 0% for any
        /// time point outside of visible ranges. Otherwise it is not guaranteed which values will resulting transform have.</param>
        /// <returns>Result of merging.</returns>
        MergeResult<Transform> MergeTransform(Transform a, TimeRange aRange, Transform b, TimeRange bRange, bool strict = false)
        {
            if (a.BlendMode != b.BlendMode)
            {
                return MergeResult<Transform>.Failed;
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
                return MergeResult<Transform>.Failed;
            }

            ShapeLayerContentArgs args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                BlendMode = a.BlendMode,
                MatchName = $"{a.MatchName}{b.MatchName}",
            };

            return new MergeResult<Transform>(new Transform(args, anchor.Value!, position.Value!, scalePercent.Value!, rotation.Value!, opacity.Value!));
        }

        MergeResult<IAnimatableVector3> MergeIAnimatableVector3(IAnimatableVector3 a, TimeRange aRange, IAnimatableVector3 b, TimeRange bRange)
        {
            if (a is AnimatableVector3 && b is AnimatableVector3)
            {
                var res = MergeAnimatable((AnimatableVector3)a, aRange, (AnimatableVector3)b, bRange);
                if (!res.Success)
                {
                    return MergeResult<IAnimatableVector3>.Failed;
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

                return new MergeResult<IAnimatableVector3>(resVector3);
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
                    return new MergeResult<IAnimatableVector3>(new AnimatableXYZ(resX.Value!, resY.Value!, resZ.Value!));
                }
            }

            return MergeResult<IAnimatableVector3>.Failed;
        }

        MergeResult<IAnimatableVector2> MergeIAnimatableVector2(IAnimatableVector2 a, TimeRange aRange, IAnimatableVector2 b, TimeRange bRange)
        {
            if (a is AnimatableVector2 && b is AnimatableVector2)
            {
                var res = MergeAnimatable((AnimatableVector2)a, aRange, (AnimatableVector2)b, bRange);
                if (!res.Success)
                {
                    return MergeResult<IAnimatableVector2>.Failed;
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

                return new MergeResult<IAnimatableVector2>(resVector2);
            }
            else if (a is AnimatableXY && b is AnimatableXY)
            {
                var aXY = (AnimatableXY)a;
                var bXY = (AnimatableXY)b;
                var resX = MergeAnimatable(aXY.X, aRange, bXY.X, bRange);
                var resY = MergeAnimatable(aXY.Y, aRange, bXY.Y, bRange);
                if (resX.Success && resY.Success)
                {
                    return new MergeResult<IAnimatableVector2>(new AnimatableXY(resX.Value!, resY.Value!));
                }
            }

            return MergeResult<IAnimatableVector2>.Failed;
        }

        /// <summary>
        /// Merge two animatables.
        /// It is not guaranteed which value this animatable will have outside of visible ranges.
        /// </summary>
        /// <param name="a">First animatable.</param>
        /// <param name="aRange">Visible range for the first animatable.</param>
        /// <param name="b">Second animatable.</param>
        /// <param name="bRange">Visible range for the second animatable.</param>
        /// <returns>Result of merging two animatables.</returns>
        MergeResult<Animatable<T>> MergeAnimatable<T>(Animatable<T> a, TimeRange aRange, Animatable<T> b, TimeRange bRange)
            where T : IEquatable<T>
        {
            if (!a.IsAnimated && !b.IsAnimated)
            {
                if (a.InitialValue.Equals(b.InitialValue))
                {
                    return new MergeResult<Animatable<T>>(new Animatable<T>(a.InitialValue));
                }

                // We do not want to introduce more animated values so just return Fail instead of trying
                // make two key frames with a.InitialValue and b.InitialValue.
                return MergeResult<Animatable<T>>.Failed;
            }

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
                mergedKeyFrames.Add(new KeyFrame<T>(aRange.Start, a.InitialValue, HoldEasing.Instance));
            }

            mergedKeyFrames.Add(new KeyFrame<T>(bRange.Start, b.InitialValue, HoldEasing.Instance));

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
                    return MergeResult<Animatable<T>>.Failed;
                }
            }

            return new MergeResult<Animatable<T>>(new Animatable<T>(mergedKeyFrames));
        }

        /// <summary>
        /// Merge two animatable opacities.
        /// It is guaranteed that opacity will be set to 0% for any point outside of visible time range.
        /// </summary>
        /// <param name="a">First animatable.</param>
        /// <param name="aRange">Visible range for the first animatable.</param>
        /// <param name="b">Second animatable.</param>
        /// <param name="bRange">Visible range for the second animatable.</param>
        /// <returns>Result of merging two animatable opacities.</returns>
        MergeResult<Animatable<Opacity>> MergeAnimatableOpacityStrict(Animatable<Opacity> a, TimeRange aRange, Animatable<Opacity> b, TimeRange bRange)
        {
            Debug.Assert(!aRange.Intersect(bRange), "Ranges should not intersect");

            if (!a.IsAnimated && !b.IsAnimated)
            {
                if (aRange.End == bRange.Start)
                {
                    if (a.InitialValue.Equals(b.InitialValue))
                    {
                        return new MergeResult<Animatable<Opacity>>(new Animatable<Opacity>(a.InitialValue));
                    }
                    else
                    {
                        var keyFrames = new List<KeyFrame<Opacity>>();
                        keyFrames.Add(new KeyFrame<Opacity>(aRange.Start, a.InitialValue, HoldEasing.Instance));
                        keyFrames.Add(new KeyFrame<Opacity>(bRange.Start, b.InitialValue, HoldEasing.Instance));
                        return new MergeResult<Animatable<Opacity>>(new Animatable<Opacity>(keyFrames));
                    }
                }
                else
                {
                    var keyFrames = new List<KeyFrame<Opacity>>();
                    keyFrames.Add(new KeyFrame<Opacity>(aRange.Start, a.InitialValue, HoldEasing.Instance));
                    keyFrames.Add(new KeyFrame<Opacity>(aRange.End, Opacity.Transparent, HoldEasing.Instance));
                    keyFrames.Add(new KeyFrame<Opacity>(bRange.Start, b.InitialValue, HoldEasing.Instance));
                    return new MergeResult<Animatable<Opacity>>(new Animatable<Opacity>(keyFrames));
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
                mergedKeyFrames.Add(new KeyFrame<Opacity>(aRange.Start, a.InitialValue, HoldEasing.Instance));
            }

            if (aRange.End < bRange.Start)
            {
                mergedKeyFrames.Add(new KeyFrame<Opacity>(aRange.End, Opacity.Transparent, HoldEasing.Instance));
            }

            if (b.IsAnimated)
            {
                if (b.KeyFrames[0].Frame != bRange.Start)
                {
                    mergedKeyFrames.Add(new KeyFrame<Opacity>(bRange.Start, b.InitialValue, HoldEasing.Instance));
                }

                foreach (var kf in b.KeyFrames)
                {
                    mergedKeyFrames.Add(new KeyFrame<Opacity>(kf.Frame, kf.Value, kf.SpatialBezier, kf.Easing));
                }
            }
            else
            {
                mergedKeyFrames.Add(new KeyFrame<Opacity>(bRange.Start, b.InitialValue, HoldEasing.Instance));
            }

            // Ensure that keyframes are in correct order!
            for (int i = 0; i + 1 < mergedKeyFrames.Count; i++)
            {
                if (mergedKeyFrames[i].Frame > mergedKeyFrames[i + 1].Frame)
                {
                    return MergeResult<Animatable<Opacity>>.Failed;
                }
            }

            return new MergeResult<Animatable<Opacity>>(new Animatable<Opacity>(mergedKeyFrames));
        }

        MergeResult<ShapeGroup> MergeShapeGroup(ShapeGroup a, TimeRange aRange, ShapeGroup b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode)
            {
                return MergeResult<ShapeGroup>.Failed;
            }

            List<ShapeLayerContent> contents = new List<ShapeLayerContent>();

            for (int i = 0; i < a.Contents.Count; i++)
            {
                var res = MergeShapeLayerContents(a.Contents[i], aRange, b.Contents[i], bRange);
                if (!res.Success)
                {
                    return MergeResult<ShapeGroup>.Failed;
                }

                contents.Add(res.Value!);
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            return new MergeResult<ShapeGroup>(new ShapeGroup(args, contents));
        }

        MergeResult<Path> MergePaths(Path a, TimeRange aRange, Path b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode || a.DrawingDirection != b.DrawingDirection)
            {
                return MergeResult<Path>.Failed;
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
                return MergeResult<Path>.Failed;
            }

            return new MergeResult<Path>(new Path(args, a.DrawingDirection, geometryData.Value!));
        }

        MergeResult<Rectangle> MergeRectangles(Rectangle a, TimeRange aRange, Rectangle b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode || a.DrawingDirection != b.DrawingDirection)
            {
                return MergeResult<Rectangle>.Failed;
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            var position = MergeIAnimatableVector3(a.Position, aRange, b.Position, bRange);
            var size = MergeIAnimatableVector3(a.Size, aRange, b.Size, bRange);
            var roundness = MergeAnimatable(a.Roundness, aRange, b.Roundness, bRange);

            if (!position.Success || !size.Success || !roundness.Success)
            {
                return MergeResult<Rectangle>.Failed;
            }

            return new MergeResult<Rectangle>(new Rectangle(args, a.DrawingDirection, position.Value!, size.Value!, roundness.Value!));
        }

        MergeResult<TrimPath> MergeTrimPaths(TrimPath a, TimeRange aRange, TrimPath b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode || a.TrimPathType != b.TrimPathType)
            {
                return MergeResult<TrimPath>.Failed;
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            var start = MergeAnimatable(a.Start, aRange, b.Start, bRange);
            var end = MergeAnimatable(a.End, aRange, b.End, bRange);
            var offset = MergeAnimatable(a.Offset, aRange, b.Offset, bRange);

            if (!start.Success || !end.Success || !offset.Success)
            {
                return MergeResult<TrimPath>.Failed;
            }

            return new MergeResult<TrimPath>(new TrimPath(args, a.TrimPathType, start.Value!, end.Value!, offset.Value!));
        }

        MergeResult<RoundCorners> MergeRoundCorners(RoundCorners a, TimeRange aRange, RoundCorners b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode)
            {
                return MergeResult<RoundCorners>.Failed;
            }

            var args = new ShapeLayerContentArgs
            {
                Name = $"{a.Name} {b.Name}",
                MatchName = $"{a.MatchName}{b.MatchName}",
                BlendMode = a.BlendMode,
            };

            var radius = MergeAnimatable(a.Radius, aRange, b.Radius, bRange);

            if (!radius.Success)
            {
                return MergeResult<RoundCorners>.Failed;
            }

            return new MergeResult<RoundCorners>(new RoundCorners(args, radius.Value!));
        }

        MergeResult<Ellipse> MergeEllipses(Ellipse a, TimeRange aRange, Ellipse b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode || a.DrawingDirection != b.DrawingDirection)
            {
                return MergeResult<Ellipse>.Failed;
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
                return MergeResult<Ellipse>.Failed;
            }

            return new MergeResult<Ellipse>(new Ellipse(args, a.DrawingDirection, positionMergeRes.Value!, diameterMergeRes.Value!));
        }

        MergeResult<LinearGradientFill> MergeLinearGradientFills(LinearGradientFill a, TimeRange aRange, LinearGradientFill b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode || a.FillType != b.FillType)
            {
                return MergeResult<LinearGradientFill>.Failed;
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
                return MergeResult<LinearGradientFill>.Failed;
            }

            return new MergeResult<LinearGradientFill>(new LinearGradientFill(args, a.FillType, opacity.Value!, startPoint.Value!, endPoint.Value!, gradientStops.Value!));
        }

        MergeResult<SolidColorFill> MergeSolidColorFills(SolidColorFill a, TimeRange aRange, SolidColorFill b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode || a.FillType != b.FillType)
            {
                return MergeResult<SolidColorFill>.Failed;
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
                return MergeResult<SolidColorFill>.Failed;
            }

            return new MergeResult<SolidColorFill>(new SolidColorFill(args, a.FillType, opacity.Value!, color.Value!));
        }

        MergeResult<SolidColorStroke> MergeSolidColorStrokes(SolidColorStroke a, TimeRange aRange, SolidColorStroke b, TimeRange bRange)
        {
            if (a.BlendMode != b.BlendMode ||
                a.CapType != b.CapType ||
                a.JoinType != b.JoinType ||
                !a.DashPattern.SequenceEqual(b.DashPattern) ||
                a.MiterLimit != b.MiterLimit)
            {
                return MergeResult<SolidColorStroke>.Failed;
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
            var strokeWidth = MergeAnimatable(a.StrokeWidth, aRange, b.StrokeWidth, bRange);
            var dashOffset = MergeAnimatable(a.DashOffset, aRange, b.DashOffset, bRange);

            if (!opacity.Success || !color.Success || !strokeWidth.Success || !dashOffset.Success)
            {
                return MergeResult<SolidColorStroke>.Failed;
            }

            return new MergeResult<SolidColorStroke>(new SolidColorStroke(
                args,
                dashOffset.Value!,
                a.DashPattern,
                color.Value!,
                opacity.Value!,
                strokeWidth.Value!,
                a.CapType,
                a.JoinType,
                a.MiterLimit));
        }

        MergeResult<ShapeLayerContent> MergeShapeLayerContents(ShapeLayerContent a, TimeRange aRange, ShapeLayerContent b, TimeRange bRange)
        {
            if (a.ContentType != b.ContentType)
            {
                return MergeResult<ShapeLayerContent>.Failed;
            }

            switch (a.ContentType)
            {
                case ShapeContentType.Group:
                    return MergeResult<ShapeLayerContent>.From(MergeShapeGroup((ShapeGroup)a, aRange, (ShapeGroup)b, bRange));
                case ShapeContentType.Path:
                    return MergeResult<ShapeLayerContent>.From(MergePaths((Path)a, aRange, (Path)b, bRange));
                case ShapeContentType.TrimPath:
                    return MergeResult<ShapeLayerContent>.From(MergeTrimPaths((TrimPath)a, aRange, (TrimPath)b, bRange));
                case ShapeContentType.Rectangle:
                    return MergeResult<ShapeLayerContent>.From(MergeRectangles((Rectangle)a, aRange, (Rectangle)b, bRange));
                case ShapeContentType.RoundCorners:
                    return MergeResult<ShapeLayerContent>.From(MergeRoundCorners((RoundCorners)a, aRange, (RoundCorners)b, bRange));
                case ShapeContentType.Ellipse:
                    return MergeResult<ShapeLayerContent>.From(MergeEllipses((Ellipse)a, aRange, (Ellipse)b, bRange));
                case ShapeContentType.LinearGradientFill:
                    return MergeResult<ShapeLayerContent>.From(MergeLinearGradientFills((LinearGradientFill)a, aRange, (LinearGradientFill)b, bRange));
                case ShapeContentType.Transform:
                    return MergeResult<ShapeLayerContent>.From(MergeTransform((Transform)a, aRange, (Transform)b, bRange));
                case ShapeContentType.SolidColorFill:
                    return MergeResult<ShapeLayerContent>.From(MergeSolidColorFills((SolidColorFill)a, aRange, (SolidColorFill)b, bRange));
                case ShapeContentType.SolidColorStroke:
                    return MergeResult<ShapeLayerContent>.From(MergeSolidColorStrokes((SolidColorStroke)a, aRange, (SolidColorStroke)b, bRange));
            }

            return MergeResult<ShapeLayerContent>.Failed;
        }

        LayerArgs CopyArgsAndClamp(Layer layer, TimeRange range)
        {
            var args = layer.CopyArgs();

            args.InFrame = Math.Clamp(args.InFrame, range.Start, range.End - 1e-6);
            args.OutFrame = Math.Clamp(args.OutFrame, range.Start + 1e-6, range.End);

            return args;
        }

        Layer ClampLayer(Layer layer, TimeRange range)
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

        MergeResult<LayerCollection> MergeLayerCollections(LayerCollection a, TimeRange aParentRange, LayerCollection b, TimeRange bParentRange)
        {
            // We need layers in Top-To-Bottom order here.
            var aLayers = a.GetLayersBottomToTop().Select(layer => ClampLayer(layer, aParentRange)).Reverse().ToList();
            var bLayers = b.GetLayersBottomToTop().Select(layer => ClampLayer(layer, bParentRange)).Reverse().ToList();

            // Here we are assigning new indices while preserving relative order.
            var aMapping = new LayersIndexMapper();
            var bMapping = new LayersIndexMapper();

            var generator = new LayersIndexMapper.IndexGenerator();

            // Layers in aLayers will have indices from 0 to aLayers.Count - 1
            foreach (var layer in aLayers)
            {
                aMapping.SetMapping(layer.Index, generator.GenerateIndex());
            }

            // Layers in bLayers will have indices from aLayers.Count to aLayers.Count + bLayers.Count - 1
            foreach (var layer in bLayers)
            {
                bMapping.SetMapping(layer.Index, generator.GenerateIndex());
            }

            aLayers = aMapping.RemapLayers(aLayers);

            bLayers = bMapping.RemapLayers(bLayers);

            var aLayerGroups = LayerGroup.LayersToLayerGroups(aLayers, canBeMergedFunc: (Layer mainLayer, Layer? matteLayer) =>
            {
                return mainLayer.OutPoint >= aParentRange.End;
            });

            var bLayerGroups = LayerGroup.LayersToLayerGroups(bLayers, canBeMergedFunc: (Layer mainLayer, Layer? matteLayer) =>
            {
                return mainLayer.InPoint <= bParentRange.Start;
            });

            var layerGroups = new List<LayerGroup>();
            layerGroups.AddRange(aLayerGroups);
            layerGroups.AddRange(bLayerGroups);

            // Optimize while we can.
            while (MakeOptimizerPass(layerGroups))
            {
                // Keep optimizing!
            }

            var layers = LayerGroup.LayerGroupsToLayers(layerGroups);

            // Score for merging two layer collections is the number of merged layers
            // divided by number of layers in smaller collection.
            double intersectionOverMinimumScore =
                (aLayers.Count + bLayers.Count - layers.Count) * 1.0 / Math.Min(aLayers.Count, bLayers.Count);

            // Layers are stored in Bottom-To-Top order.
            layers.Reverse();

            return new MergeResult<LayerCollection>(new LayerCollection(layers), intersectionOverMinimumScore);
        }

        MergeResult<LayerCollectionAsset> MergeLayerCollectionAssets(LayerCollectionAsset a, TimeRange aParentRange, LayerCollectionAsset b, TimeRange bParentRange)
        {
            var layerCollection = MergeLayerCollections(a.Layers, aParentRange, b.Layers, bParentRange);

            if (!layerCollection.Success)
            {
                return MergeResult<LayerCollectionAsset>.Failed;
            }

            return new MergeResult<LayerCollectionAsset>(new LayerCollectionAsset($"{a.Id} {b.Id} {AssetsGenerated.Count}", layerCollection.Value!), layerCollection.Score);
        }

        /// <summary>
        /// Merge two assets that are not intersecting into one layer.
        /// </summary>
        /// <param name="a">First asset.</param>
        /// <param name="aParentRange">Visible time range for the first layer. Everything that is outside of this range will be invisible.</param>
        /// <param name="b">Second asset.</param>
        /// <param name="bParentRange">Visible time range for the second layer. Everything that is outside of this range will be invisible.</param>
        /// <returns>Result of merging two assets.</returns>
        MergeResult<Asset> MergeAssets(Asset a, TimeRange aParentRange, Asset b, TimeRange bParentRange)
        {
            if (a.Type != b.Type)
            {
                return MergeResult<Asset>.Failed;
            }

            switch (a.Type)
            {
                case Asset.AssetType.LayerCollection:
                    return MergeResult<Asset>.From(MergeLayerCollectionAssets((LayerCollectionAsset)a, aParentRange, (LayerCollectionAsset)b, bParentRange));
            }

            return MergeResult<Asset>.Failed;
        }

        public MergeResult<LayerGroup> MergeLayerGroups(LayerGroup a, LayerGroup b)
        {
            if (!a.CanBeMerged || !b.CanBeMerged)
            {
                return MergeResult<LayerGroup>.Failed;
            }

            var mainLayerMergeRes = MergeLayers(a.MainLayer, b.MainLayer);

            if (!mainLayerMergeRes.Success)
            {
                return MergeResult<LayerGroup>.Failed;
            }

            if (a.MatteLayer is null && b.MatteLayer is null)
            {
                return new MergeResult<LayerGroup>(new LayerGroup(mainLayerMergeRes.Value!, canBeMerged: false));
            }

            if (a.MatteLayer is null || b.MatteLayer is null)
            {
                // TODO: probably we can still merge in this case too.
                return MergeResult<LayerGroup>.Failed;
            }

            bool canIgnoreParent = a.MainLayer.Index == a.MatteLayer.Parent && b.MainLayer.Index == b.MatteLayer.Parent;
            var matteLayerMergeRes = MergeLayers(a.MatteLayer, b.MatteLayer, canIgnoreParent);

            if (!matteLayerMergeRes.Success)
            {
                return MergeResult<LayerGroup>.Failed;
            }

            return new MergeResult<LayerGroup>(new LayerGroup(mainLayerMergeRes.Value!, matteLayerMergeRes.Value!, canBeMerged: false));
        }

        /// <summary>
        /// Merge two layers which time ranges are not intersecting into one layer.
        /// </summary>
        /// <param name="a">First layer to merge.</param>
        /// <param name="b">Second layer to merge.</param>
        /// <param name="ignoreParent">Pass true if you want to ignore Parent equal check, use if you are sure that parent will be the same after the merge.</param>
        /// <returns>Result of merging two layers.</returns>
        public MergeResult<Layer> MergeLayers(Layer a, Layer b, bool ignoreParent = false)
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
                return MergeResult<Layer>.Failed;
            }

            switch (a.Type)
            {
                case LayerType.PreComp:
                    return MergeResult<Layer>.From(MergePreCompLayers((PreCompLayer)a, (PreCompLayer)b));
                case LayerType.Shape:
                    return MergeResult<Layer>.From(MergeShapeLayers((ShapeLayer)a, (ShapeLayer)b));
                case LayerType.Null:
                    return MergeResult<Layer>.From(MergeNullLayers((NullLayer)a, (NullLayer)b));
            }

            return MergeResult<Layer>.Failed;
        }

        MergeResult<ShapeLayer> MergeShapeLayers(ShapeLayer a, ShapeLayer b)
        {
            if (a.Contents.Count != b.Contents.Count)
            {
                return MergeResult<ShapeLayer>.Failed;
            }

            var transformMergeRes = MergeTransform(a.Transform, TimeRange.GetForLayer(a), b.Transform, TimeRange.GetForLayer(b));

            if (!transformMergeRes.Success)
            {
                return MergeResult<ShapeLayer>.Failed;
            }

            var args = a.CopyArgs();

            args.Name = $"{a.Name} {b.Name}";
            args.OutFrame = b.OutPoint;
            args.Transform = transformMergeRes.Value!;

            double totalScore = transformMergeRes.Score;

            List<ShapeLayerContent> contents = new List<ShapeLayerContent>();

            for (int i = 0; i < a.Contents.Count; i++)
            {
                var res = MergeShapeLayerContents(a.Contents[i], TimeRange.GetForLayer(a), b.Contents[i], TimeRange.GetForLayer(b));

                if (!res.Success)
                {
                    return MergeResult<ShapeLayer>.Failed;
                }

                contents.Add(res.Value!);
                totalScore += res.Score;
            }

            return new MergeResult<ShapeLayer>(new ShapeLayer(args, contents), totalScore);
        }

        MergeResult<NullLayer> MergeNullLayers(NullLayer a, NullLayer b)
        {
            var transformMergeRes = MergeTransform(a.Transform, TimeRange.GetForLayer(a), b.Transform, TimeRange.GetForLayer(b));

            if (!transformMergeRes.Success)
            {
                return MergeResult<NullLayer>.Failed;
            }

            var args = a.CopyArgs();

            args.Name = $"{a.Name} {b.Name}";
            args.OutFrame = b.OutPoint;
            args.Transform = transformMergeRes.Value!;

            return new MergeResult<NullLayer>(new NullLayer(args), transformMergeRes.Score);
        }

        MergeResult<PreCompLayer> MergePreCompLayers(PreCompLayer a, PreCompLayer b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return MergeResult<PreCompLayer>.Failed;
            }

            var aAsset = GetAssetById(a.RefId);

            var bAsset = GetAssetById(b.RefId);

            if (aAsset is not LayerCollectionAsset || bAsset is not LayerCollectionAsset)
            {
                return MergeResult<PreCompLayer>.Failed;
            }

            double offset = b.InPoint - a.InPoint;

            var aAssetOffset = new LayerCollectionAsset(aAsset.Id, ((LayerCollectionAsset)aAsset).Layers.WithTimeOffset(0.0));
            var bAssetOffset = new LayerCollectionAsset(bAsset.Id, ((LayerCollectionAsset)bAsset).Layers.WithTimeOffset(offset));

            var asset = MergeAssets(
                aAssetOffset!,
                TimeRange.GetForLayer(a).ShiftLeft(a.InPoint),
                bAssetOffset!,
                TimeRange.GetForLayer(b).ShiftLeft(a.InPoint)
                );

            if (!asset.Success)
            {
                return MergeResult<PreCompLayer>.Failed;
            }

            AssetsGenerated.Add(asset.Value!);

            var transformMergeRes = MergeTransform(a.Transform, TimeRange.GetForLayer(a), b.Transform, TimeRange.GetForLayer(b), true);

            if (!transformMergeRes.Success)
            {
                return MergeResult<PreCompLayer>.Failed;
            }

            var args = a.CopyArgs();

            args.Name = $"{a.Name} {b.Name}";
            args.OutFrame = b.OutPoint;
            args.Transform = transformMergeRes.Value!;

            return new MergeResult<PreCompLayer>(
                new PreCompLayer(args, asset.Value!.Id, a.Width, a.Height),
                asset.Score
                );
        }
    }
}
