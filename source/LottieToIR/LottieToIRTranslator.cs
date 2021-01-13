// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToIR
{
#if PUBLIC_LottieToIR
    public
#endif
    sealed class LottieToIRTranslator
    {
        public static IRComposition? TranslateLottieToIR(LottieData.LottieComposition from)
        {
            var translator = new LottieToIRTranslator();

            var result = new IRComposition(
                name: from.Name,
                width: from.Width,
                height: from.Height,
                inPoint: from.InPoint,
                outPoint: from.OutPoint,
                framesPerSecond: from.FramesPerSecond,
                is3d: from.Is3d,
                version: from.Version,
                assets: TranslateAssetCollection(from.Assets),
                chars: from.Chars.Select(TranslateChar),
                fonts: from.Fonts.Select(TranslateFont),
                layers: TranslateLayerCollection(from.Layers),
                markers: from.Markers.Select(TranslateMarker),
                extraData: from.ExtraData);
            return result;
        }

        static Font TranslateFont(LottieData.Font from)
            => new Font(from.Name, from.Family, from.Style, from.Ascent);

        static Marker TranslateMarker(LottieData.Marker from)
            => new Marker(from.Name, from.Frame, from.DurationInFrames);

        static AssetCollection TranslateAssetCollection(LottieData.AssetCollection from)
            => new AssetCollection(from.Select(TranslateAsset));

        static Asset TranslateAsset(LottieData.Asset from)
            => from.Type switch
            {
                LottieData.Asset.AssetType.LayerCollection => TranslateLayerCollectionAsset((LottieData.LayerCollectionAsset)from),
                LottieData.Asset.AssetType.Image => TranslateImageAsset((LottieData.ImageAsset)from),
                _ => throw Unreachable,
            };

        static LayerCollectionAsset TranslateLayerCollectionAsset(LottieData.LayerCollectionAsset from)
            => new LayerCollectionAsset(from.Id, TranslateLayerCollection(from.Layers));

        static ImageAsset TranslateImageAsset(LottieData.ImageAsset from)
            => from.ImageType switch
            {
                LottieData.ImageAsset.ImageAssetType.Embedded => TranslateEmbeddedImageAsset((LottieData.EmbeddedImageAsset)from),
                LottieData.ImageAsset.ImageAssetType.External => TranslateExternalImageAsset((LottieData.ExternalImageAsset)from),
                _ => throw Unreachable,
            };

        static EmbeddedImageAsset TranslateEmbeddedImageAsset(LottieData.EmbeddedImageAsset from)
            => new EmbeddedImageAsset(from.Id, from.Width, from.Height, from.Bytes, from.Format);

        static ExternalImageAsset TranslateExternalImageAsset(LottieData.ExternalImageAsset from)
            => new ExternalImageAsset(from.Id, from.Width, from.Height, from.Path, from.FileName);

        static LayerCollection TranslateLayerCollection(LottieData.LayerCollection from)
            => new LayerCollection(from.GetLayersBottomToTop().Select(TranslateLayer));

        static Layer TranslateLayer(LottieData.Layer from)
            => from.Type switch
            {
                LottieData.Layer.LayerType.PreComp => TranslatePreCompLayer((LottieData.PreCompLayer)from),
                LottieData.Layer.LayerType.Solid => TranslateSolidLayer((LottieData.SolidLayer)from),
                LottieData.Layer.LayerType.Image => TranslateImageLayer((LottieData.ImageLayer)from),
                LottieData.Layer.LayerType.Null => TranslateNullLayer((LottieData.NullLayer)from),
                LottieData.Layer.LayerType.Shape => TranslateShapeLayer((LottieData.ShapeLayer)from),
                LottieData.Layer.LayerType.Text => TranslateTextLayer((LottieData.TextLayer)from),
                _ => throw Unreachable,
            };

        static Layer.LayerArgs TranslateLayerArgs(LottieData.Layer from)
            => new Layer.LayerArgs
            {
                AutoOrient = from.AutoOrient,
                BlendMode = (BlendMode)from.BlendMode,
                Effects = from.Effects.Select(TranslateEffect).ToArray(),
                Index = from.Index,
                InFrame = from.InPoint,
                Is3d = from.Is3d,
                IsHidden = from.IsHidden,
                MatteType = (MatteType)from.MatteType,
                Masks = from.Masks.Select(TranslateMask).ToArray(),
                Name = from.Name,
                OutFrame = from.OutPoint,
                Parent = from.Parent,
                StartFrame = from.StartTime,
                TimeStretch = from.TimeStretch,
                Transform = TranslateTransform(from.Transform),
            };

        static PreCompLayer TranslatePreCompLayer(LottieData.PreCompLayer from)
            => new PreCompLayer(TranslateLayerArgs(from), from.RefId, from.Width, from.Height);

        static SolidLayer TranslateSolidLayer(LottieData.SolidLayer from)
            => new SolidLayer(TranslateLayerArgs(from), from.Width, from.Height, TranslateColor(from.Color));

        static ImageLayer TranslateImageLayer(LottieData.ImageLayer from)
            => new ImageLayer(TranslateLayerArgs(from), from.RefId);

        static NullLayer TranslateNullLayer(LottieData.NullLayer from)
            => new NullLayer(TranslateLayerArgs(from));

        static ShapeLayer TranslateShapeLayer(LottieData.ShapeLayer from)
            => new ShapeLayer(TranslateLayerArgs(from), from.Contents.Select(TranslateShapeLayerContent));

        static TextLayer TranslateTextLayer(LottieData.TextLayer from)
            => new TextLayer(TranslateLayerArgs(from), from.RefId);

        static IR.Char TranslateChar(LottieData.Char from)
            => new IR.Char(from.Characters, from.FontFamily, from.Style, from.FontSize, from.Width, from.Shapes.Select(TranslateShapeLayerContent));

        static Effect TranslateEffect(LottieData.Effect from)
            => from.Type switch
            {
                LottieData.Effect.EffectType.DropShadow => TranslateDropShadowEffect((LottieData.DropShadowEffect)from),
                LottieData.Effect.EffectType.GaussianBlur => TranslateGaussianBlurEffect((LottieData.GaussianBlurEffect)from),
                _ => new Effect.Unknown((double)from.Type, from.Name, from.IsEnabled),
            };

        static Mask TranslateMask(LottieData.Mask from)
            => new Mask(
                from.Inverted,
                from.Name,
                TranslateAnimatable(from.Points, TranslatePathGeometry),
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                (Mask.MaskMode)from.Mode);

        static Color TranslateColor(LottieData.Color from)
            => Color.FromArgb(from.A, from.R, from.G, from.B);

        static Opacity TranslateOpacity(LottieData.Opacity from)
            => Opacity.FromFloat(from.Value);

        static Rotation TranslateRotation(LottieData.Rotation from)
            => Rotation.FromDegrees(from.Degrees);

        static Trim TranslateTrim(LottieData.Trim from)
            => Trim.FromFloat(from.Value);

        static BezierSegment TranslateBezierSegment(LottieData.BezierSegment from)
            => new BezierSegment(
                TranslateVector2(from.ControlPoint0),
                TranslateVector2(from.ControlPoint1),
                TranslateVector2(from.ControlPoint2),
                TranslateVector2(from.ControlPoint3));

        static Vector2 TranslateVector2(LottieData.Vector2 from)
            => new Vector2(from.X, from.Y);

        static Vector3 TranslateVector3(LottieData.Vector3 from)
            => new Vector3(from.X, from.Y, from.Z);

        static PathGeometry TranslatePathGeometry(LottieData.PathGeometry from)
            => new PathGeometry(new Sequence<BezierSegment>(from.BezierSegments.Select(TranslateBezierSegment)), from.IsClosed);

        static DropShadowEffect TranslateDropShadowEffect(LottieData.DropShadowEffect from)
            => new DropShadowEffect(
                from.Name,
                from.IsEnabled,
                TranslateAnimatable(from.Color, TranslateColor),
                TranslateAnimatable(from.Direction, TranslateRotation),
                TranslateAnimatable(from.Distance),
                TranslateAnimatable(from.IsShadowOnly),
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.Softness));

        static GaussianBlurEffect TranslateGaussianBlurEffect(LottieData.GaussianBlurEffect from)
             => new GaussianBlurEffect(
                 from.Name,
                 from.IsEnabled,
                 TranslateAnimatable(from.Blurriness),
                 TranslateAnimatable(from.BlurDimensions, TranslateBlurDimension),
                 TranslateAnimatable(from.RepeatEdgePixels),
                 from.ForceGpuRendering);

        static Enum<BlurDimension> TranslateBlurDimension(LottieData.Enum<LottieData.BlurDimension> from)
            => (BlurDimension)from.Value;

        static Easing TranslateEasing(LottieData.Easing from)
            => from.Type switch
            {
                LottieData.Easing.EasingType.CubicBezier => TranslateCubicBezierEasing((LottieData.CubicBezierEasing)from),
                LottieData.Easing.EasingType.Hold => HoldEasing.Instance,
                LottieData.Easing.EasingType.Linear => LinearEasing.Instance,
                _ => throw Unreachable,
            };

        static CubicBezierEasing TranslateCubicBezierEasing(LottieData.CubicBezierEasing from)
            => new CubicBezierEasing(from.Beziers.Select(TranslateCubicBezier));

        static CubicBezier TranslateCubicBezier(LottieData.CubicBezier from)
            => new CubicBezier(TranslateVector2(from.ControlPoint1), TranslateVector2(from.ControlPoint2));

        static KeyFrame<T> TranslateKeyFrame<T>(LottieData.KeyFrame<T> from)
            where T : IEquatable<T>
            => TranslateKeyFrame(from, kf => kf);

        static KeyFrame<T> TranslateKeyFrame<T, TFrom>(LottieData.KeyFrame<TFrom> from, Func<TFrom, T> selector)
            where T : IEquatable<T>
            where TFrom : IEquatable<TFrom>
            => new KeyFrame<T>(
                from.Frame,
                selector(from.Value),
                from.SpatialBezier.HasValue ? TranslateCubicBezier(from.SpatialBezier.Value) : null,
                TranslateEasing(from.Easing));

        static Animatable<T> TranslateAnimatable<T>(LottieData.Animatable<T> from)
            where T : IEquatable<T>
            => from.IsAnimated
            ? new Animatable<T>(from.KeyFrames.Select(TranslateKeyFrame), from.PropertyIndex)
            : new Animatable<T>(from.InitialValue, from.PropertyIndex);

        static Animatable<double>? TranslateAnimatableNullable(LottieData.Animatable<double>? from)
            => from is null ? null : TranslateAnimatable(from);

        static Animatable<T> TranslateAnimatable<T, TFrom>(LottieData.Animatable<TFrom> from, Func<TFrom, T> selector)
            where T : IEquatable<T>
            where TFrom : IEquatable<TFrom>
            => from.IsAnimated
            ? new Animatable<T>(from.KeyFrames.Select(kf => TranslateKeyFrame(kf, selector)), from.PropertyIndex)
            : new Animatable<T>(selector(from.InitialValue), from.PropertyIndex);

        static IAnimatableVector3 TranslateAnimatable(LottieData.IAnimatableVector3 from)
            => from.Type switch
            {
                LottieData.AnimatableVector3Type.Vector3 => TranslateAnimatableVector3((LottieData.AnimatableVector3)from),
                LottieData.AnimatableVector3Type.XYZ => TranslateAnimatableXYZ((LottieData.AnimatableXYZ)from),
                _ => throw Unreachable,
            };

        static AnimatableVector3 TranslateAnimatableVector3(LottieData.AnimatableVector3 from)
            => from.IsAnimated
            ? new AnimatableVector3(from.KeyFrames.Select(kf => TranslateKeyFrame(kf, TranslateVector3)), from.PropertyIndex)
            : new AnimatableVector3(TranslateVector3(from.InitialValue), from.PropertyIndex);

        static AnimatableXYZ TranslateAnimatableXYZ(LottieData.AnimatableXYZ from)
            => new AnimatableXYZ(TranslateAnimatable(from.X), TranslateAnimatable(from.Y), TranslateAnimatable(from.Z));

        static ShapeLayerContent TranslateShapeLayerContent(LottieData.ShapeLayerContent from)
         =>
            from.ContentType switch
            {
                LottieData.ShapeContentType.Ellipse => TranslateEllipse((LottieData.Ellipse)from),
                LottieData.ShapeContentType.Group => TranslateShapeGroup((LottieData.ShapeGroup)from),
                LottieData.ShapeContentType.LinearGradientFill => TranslateLinearGradientFill((LottieData.LinearGradientFill)from),
                LottieData.ShapeContentType.LinearGradientStroke => TranslateLinearGradientStroke((LottieData.LinearGradientStroke)from),
                LottieData.ShapeContentType.MergePaths => TranslateMergePaths((LottieData.MergePaths)from),
                LottieData.ShapeContentType.Path => TranslatePath((LottieData.Path)from),
                LottieData.ShapeContentType.Polystar => TranslatePolystar((LottieData.Polystar)from),
                LottieData.ShapeContentType.RadialGradientFill => TranslateRadialGradientFill((LottieData.RadialGradientFill)from),
                LottieData.ShapeContentType.RadialGradientStroke => TranslateRadialGradientStroke((LottieData.RadialGradientStroke)from),
                LottieData.ShapeContentType.Rectangle => TranslateRectangle((LottieData.Rectangle)from),
                LottieData.ShapeContentType.Repeater => TranslateRepeater((LottieData.Repeater)from),
                LottieData.ShapeContentType.RoundCorners => TranslateRoundCorners((LottieData.RoundCorners)from),
                LottieData.ShapeContentType.SolidColorFill => TranslateSolidColorFill((LottieData.SolidColorFill)from),
                LottieData.ShapeContentType.SolidColorStroke => TranslateSolidColorStroke((LottieData.SolidColorStroke)from),
                LottieData.ShapeContentType.Transform => TranslateTransform((LottieData.Transform)from),
                LottieData.ShapeContentType.TrimPath => TranslateTrimPath((LottieData.TrimPath)from),
                _ => throw Unreachable,
            };

        static ShapeLayerContent.ShapeLayerContentArgs TranslateShapeLayerContentArgs(LottieData.ShapeLayerContent from)
            => new ShapeLayerContent.ShapeLayerContentArgs
            {
                BlendMode = (BlendMode)from.BlendMode,
                MatchName = from.MatchName,
                Name = from.Name,
            };

        static Ellipse TranslateEllipse(LottieData.Ellipse from)
            => new Ellipse(
                TranslateShapeLayerContentArgs(from),
                (DrawingDirection)from.DrawingDirection,
                TranslateAnimatable(from.Position),
                TranslateAnimatable(from.Diameter));

        static ShapeGroup TranslateShapeGroup(LottieData.ShapeGroup from)
            => new ShapeGroup(
                TranslateShapeLayerContentArgs(from),
                from.Contents.Select(TranslateShapeLayerContent));

        static GradientStop TranslateGradientStop(LottieData.GradientStop from)
            => from.Kind switch
            {
                LottieData.GradientStop.GradientStopKind.Color => new ColorGradientStop(from.Offset, TranslateColor(((LottieData.ColorGradientStop)from).Color)),
                LottieData.GradientStop.GradientStopKind.Opacity => new OpacityGradientStop(from.Offset, TranslateOpacity(((LottieData.OpacityGradientStop)from).Opacity)),
                _ => throw Unreachable,
            };

        static LinearGradientFill TranslateLinearGradientFill(LottieData.LinearGradientFill from)
            => new LinearGradientFill(
                TranslateShapeLayerContentArgs(from),
                (ShapeFill.PathFillType)from.FillType,
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.StartPoint),
                TranslateAnimatable(from.EndPoint),
                TranslateAnimatable(from.GradientStops, s => TranslateSequence(s, TranslateGradientStop)));

        static LinearGradientStroke TranslateLinearGradientStroke(LottieData.LinearGradientStroke from)
            => new LinearGradientStroke(
                TranslateShapeLayerContentArgs(from),
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.StrokeWidth),
                (ShapeStroke.LineCapType)from.CapType,
                (ShapeStroke.LineJoinType)from.JoinType,
                from.MiterLimit,
                TranslateAnimatable(from.StartPoint),
                TranslateAnimatable(from.EndPoint),
                TranslateAnimatable(from.GradientStops, s => TranslateSequence(s, TranslateGradientStop)));

        static MergePaths TranslateMergePaths(LottieData.MergePaths from)
            => new MergePaths(
                TranslateShapeLayerContentArgs(from),
                (MergePaths.MergeMode)from.Mode);

        static Path TranslatePath(LottieData.Path from)
            => new Path(
                TranslateShapeLayerContentArgs(from),
                (DrawingDirection)from.DrawingDirection,
                TranslateAnimatable(from.Data, TranslatePathGeometry));

        static Polystar TranslatePolystar(LottieData.Polystar from)
            => new Polystar(
                TranslateShapeLayerContentArgs(from),
                (DrawingDirection)from.DrawingDirection,
                (Polystar.PolyStarType)from.StarType,
                TranslateAnimatable(from.Points),
                TranslateAnimatable(from.Position),
                TranslateAnimatable(from.Rotation),
                TranslateAnimatableNullable(from.InnerRadius),
                TranslateAnimatable(from.OuterRadius),
                TranslateAnimatableNullable(from.InnerRoundness),
                TranslateAnimatable(from.OuterRoundness));

        static RadialGradientFill TranslateRadialGradientFill(LottieData.RadialGradientFill from)
            => new RadialGradientFill(
                TranslateShapeLayerContentArgs(from),
                (ShapeFill.PathFillType)from.FillType,
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.StartPoint),
                TranslateAnimatable(from.EndPoint),
                TranslateAnimatable(from.GradientStops, s => TranslateSequence(s, TranslateGradientStop)),
                TranslateAnimatable(from.HighlightLength),
                TranslateAnimatable(from.HighlightDegrees));

        static RadialGradientStroke TranslateRadialGradientStroke(LottieData.RadialGradientStroke from)
            => new RadialGradientStroke(
                TranslateShapeLayerContentArgs(from),
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.StrokeWidth),
                (ShapeStroke.LineCapType)from.CapType,
                (ShapeStroke.LineJoinType)from.JoinType,
                from.MiterLimit,
                TranslateAnimatable(from.StartPoint),
                TranslateAnimatable(from.EndPoint),
                TranslateAnimatable(from.GradientStops, s => TranslateSequence(s, TranslateGradientStop)),
                TranslateAnimatable(from.HighlightLength),
                TranslateAnimatable(from.HighlightDegrees));

        static Rectangle TranslateRectangle(LottieData.Rectangle from)
            => new Rectangle(
                TranslateShapeLayerContentArgs(from),
                (DrawingDirection)from.DrawingDirection,
                TranslateAnimatable(from.Position),
                TranslateAnimatable(from.Size),
                TranslateAnimatable(from.Roundness));

        static Repeater TranslateRepeater(LottieData.Repeater from)
            => new Repeater(
                TranslateShapeLayerContentArgs(from),
                TranslateAnimatable(from.Count),
                TranslateAnimatable(from.Offset),
                TranslateRepeaterTransform(from.Transform));

        static RoundCorners TranslateRoundCorners(LottieData.RoundCorners from)
            => new RoundCorners(
                TranslateShapeLayerContentArgs(from),
                TranslateAnimatable(from.Radius));

        static Sequence<T> TranslateSequence<T>(LottieData.Sequence<T> from)
            => new Sequence<T>(from);

        static Sequence<T> TranslateSequence<T, TFrom>(LottieData.Sequence<TFrom> from, Func<TFrom, T> selector)
            => new Sequence<T>(from.Select(selector));

        static SolidColorFill TranslateSolidColorFill(LottieData.SolidColorFill from)
            => new SolidColorFill(
                TranslateShapeLayerContentArgs(from),
                (ShapeFill.PathFillType)from.FillType,
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.Color, TranslateColor));

        static SolidColorStroke TranslateSolidColorStroke(LottieData.SolidColorStroke from)
            => new SolidColorStroke(
                TranslateShapeLayerContentArgs(from),
                TranslateAnimatable(from.DashOffset),
                from.DashPattern,
                TranslateAnimatable(from.Color, TranslateColor),
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.StrokeWidth),
                (ShapeStroke.LineCapType)from.CapType,
                (ShapeStroke.LineJoinType)from.JoinType,
                from.MiterLimit);

        static Transform TranslateTransform(LottieData.Transform from)
             => new Transform(
                 TranslateShapeLayerContentArgs(from),
                 TranslateAnimatable(from.Anchor),
                 TranslateAnimatable(from.Position),
                 TranslateAnimatable(from.ScalePercent),
                 TranslateAnimatable(from.Rotation, TranslateRotation),
                 TranslateAnimatable(from.Opacity, TranslateOpacity));

        static TrimPath TranslateTrimPath(LottieData.TrimPath from)
            => new TrimPath(
                TranslateShapeLayerContentArgs(from),
                (TrimPath.TrimType)from.TrimPathType,
                TranslateAnimatable(from.Start, TranslateTrim),
                TranslateAnimatable(from.End, TranslateTrim),
                TranslateAnimatable(from.Offset, TranslateRotation));

        static RepeaterTransform TranslateRepeaterTransform(LottieData.RepeaterTransform from)
            => new RepeaterTransform(
                TranslateShapeLayerContentArgs(from),
                TranslateAnimatable(from.Anchor),
                TranslateAnimatable(from.Position),
                TranslateAnimatable(from.ScalePercent),
                TranslateAnimatable(from.Rotation, TranslateRotation),
                TranslateAnimatable(from.Opacity, TranslateOpacity),
                TranslateAnimatable(from.StartOpacity, TranslateOpacity),
                TranslateAnimatable(from.EndOpacity, TranslateOpacity));

        // The code we hit is supposed to be unreachable. This indicates a bug.
        static Exception Unreachable => new InvalidOperationException("Unreachable code executed");
    }
}