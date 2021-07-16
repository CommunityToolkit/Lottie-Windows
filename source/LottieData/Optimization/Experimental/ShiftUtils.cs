﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Layer;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.ShapeLayerContent;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization
{
    /// <summary>
    /// Set of methods to shift time range of different Lottie objects. Used by optimizer.
    /// </summary>
    static class ShiftUtils
    {
        static Animatable<T> ShiftAnimatable<T>(Animatable<T> a, double shift)
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

        static IAnimatableVector3 ShiftIAnimatableVector3(IAnimatableVector3 a, double shift)
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

        static IAnimatableVector2 ShiftIAnimatableVector2(IAnimatableVector2 a, double shift)
        {
            if (a is AnimatableVector2)
            {
                var v = ShiftAnimatable((AnimatableVector2)a, shift);
                return v.IsAnimated ? new AnimatableVector2(v.KeyFrames) : new AnimatableVector2(v.InitialValue);
            }

            Debug.Assert(a is AnimatableXY, "There are only AnimatableXY and AnimatableVector2 implementations");

            var aXY = (AnimatableXY)a;
            var resX = ShiftAnimatable(aXY.X, shift);
            var resY = ShiftAnimatable(aXY.Y, shift);

            return new AnimatableXY(resX, resY);
        }

        static Transform ShiftTransform(Transform a, double shift)
        {
            return new Transform(
                a.CopyArgs(),
                ShiftIAnimatableVector3(a.Anchor, shift),
                ShiftIAnimatableVector3(a.Position, shift),
                ShiftIAnimatableVector3(a.ScalePercent, shift),
                ShiftAnimatable(a.Rotation, shift),
                ShiftAnimatable(a.Opacity, shift)
                );
        }

        static Path ShiftPath(Path path, double shift)
        {
            return new Path(path.CopyArgs(), path.DrawingDirection, ShiftAnimatable(path.Data, shift));
        }

        static Ellipse ShiftEllipse(Ellipse ellipse, double shift)
        {
            return new Ellipse(
                ellipse.CopyArgs(),
                ellipse.DrawingDirection,
                ShiftIAnimatableVector3(ellipse.Position, shift),
                ShiftIAnimatableVector3(ellipse.Diameter, shift)
                );
        }

        static SolidColorFill ShiftSolidColorFill(SolidColorFill solidColorFill, double shift)
        {
            return new SolidColorFill(
                solidColorFill.CopyArgs(),
                solidColorFill.FillType,
                ShiftAnimatable(solidColorFill.Opacity, shift),
                ShiftAnimatable(solidColorFill.Color, shift)
                );
        }

        static LinearGradientFill ShiftLinearGradientFill(LinearGradientFill linearGradientFill, double shift)
        {
            return new LinearGradientFill(
                linearGradientFill.CopyArgs(),
                linearGradientFill.FillType,
                ShiftAnimatable(linearGradientFill.Opacity, shift),
                ShiftIAnimatableVector2(linearGradientFill.StartPoint, shift),
                ShiftIAnimatableVector2(linearGradientFill.EndPoint, shift),
                ShiftAnimatable(linearGradientFill.GradientStops, shift)
                );
        }

        static ShapeLayerContent? ShiftShapeLayerContent(ShapeLayerContent a, double shift)
        {
            switch (a.ContentType)
            {
                case ShapeContentType.Path:
                    return ShiftPath((Path)a, shift);
                case ShapeContentType.Ellipse:
                    return ShiftEllipse((Ellipse)a, shift);
                case ShapeContentType.SolidColorFill:
                    return ShiftSolidColorFill((SolidColorFill)a, shift);
                case ShapeContentType.LinearGradientFill:
                    return ShiftLinearGradientFill((LinearGradientFill)a, shift);
            }

            return null;
        }

        static ShapeLayer? ShiftShapeLayer(ShapeLayer a, double shift)
        {
            var args = a.CopyArgs();

            args.Transform = ShiftTransform(a.Transform, shift);
            args.StartFrame += shift;
            args.InFrame += shift;
            args.OutFrame += shift;

            var contents = a.Contents.Select(content => ShiftShapeLayerContent(content, shift));

            if (contents.Any(contnet => contents is null))
            {
                return null;
            }

            return new ShapeLayer(args, contents.Select(content => content!));
        }

        static NullLayer ShiftNullLayer(NullLayer a, double shift)
        {
            var args = a.CopyArgs();

            args.Transform = ShiftTransform(a.Transform, shift);
            args.StartFrame += shift;
            args.InFrame += shift;
            args.OutFrame += shift;

            return new NullLayer(args);
        }

        static Layer? ShiftLayer(Layer a, double shift)
        {
            switch (a.Type)
            {
                case LayerType.Shape:
                    return ShiftShapeLayer((ShapeLayer)a, shift);
                case LayerType.Null:
                    return ShiftNullLayer((NullLayer)a, shift);

                    // TODO: Implement for other types.
            }

            return null;
        }

        static LayerCollection? ShiftLayerCollection(LayerCollection a, double shift)
        {
            var layers = a.GetLayersBottomToTop();

            var layersAfterShift = new List<Layer>();

            foreach (var layer in layers)
            {
                var layerShifted = ShiftLayer(layer, shift);

                if (layerShifted is null)
                {
                    return null;
                }

                layersAfterShift.Add(layerShifted!);
            }

            return new LayerCollection(layersAfterShift);
        }

        static LayerCollectionAsset? ShiftLayerCollectionAsset(LayerCollectionAsset a, double shift)
        {
            var layerCollectionShifted = ShiftLayerCollection(a.Layers, shift);

            if (layerCollectionShifted is null)
            {
                return null;
            }

            return new LayerCollectionAsset(a.Id, layerCollectionShifted!);
        }

        public static Asset? ShiftAsset(Asset a, double shift)
        {
            switch (a.Type)
            {
                case Asset.AssetType.LayerCollection:
                    return ShiftLayerCollectionAsset((LayerCollectionAsset)a, shift);

                    // TODO: Implement for other types.
            }

            return null;
        }
    }
}
