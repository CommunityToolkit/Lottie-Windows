// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Treeful;
using Microsoft.Toolkit.Uwp.UI.Lottie.YamlData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Serialization
{
#if PUBLIC_IR
    public
#endif
    sealed class IRYamlSerializer : YamlFactory
    {
        public static void WriteYaml(IRComposition root, TextWriter writer, string? comment = null)
        {
            var serializer = new IRYamlSerializer();

            // Convert the LottieComposition into the Yaml data model.
            var yaml = serializer.FromLottieObject(root);

            var yamlWriter = new YamlWriter(writer);
            var documentStart = "--- !LottieComposition";
            if (!string.IsNullOrWhiteSpace(comment))
            {
                documentStart += $" # {comment}";
            }

            yamlWriter.Write(documentStart);
            yamlWriter.WriteObject(yaml);
        }

        YamlMap GetLottieObjectContent(IRObject obj)
        {
            var name = obj.Name;

            var result = new YamlMap
            {
                { nameof(obj.Name), name },
            };

            if (name is string)
            {
                result.Comment = name;
            }

            return result;
        }

        void AddFromIGradient(IGradient content, YamlMap result)
        {
            result.Add(nameof(content.StartPoint), FromAnimatable(content.StartPoint));
            result.Add(nameof(content.EndPoint), FromAnimatable(content.EndPoint));
            result.Add(nameof(content.GradientStops), FromAnimatable(content.GradientStops, p => FromSequence(p, FromGradientStop)));
        }

        void AddFromIRadialGradient(IRadialGradient content, YamlMap result)
        {
            AddFromIGradient(content, result);
            result.Add(nameof(content.HighlightDegrees), FromAnimatable(content.HighlightDegrees));
            result.Add(nameof(content.HighlightLength), FromAnimatable(content.HighlightLength));
        }

        YamlObject FromLottieObject(IRObject obj)
        {
            var superclassContent = GetLottieObjectContent(obj);

            switch (obj.ObjectType)
            {
                case IRObjectType.Effect:
                    return FromEffect((Effect)obj, superclassContent);

                case IRObjectType.Layer:
                    return FromLayer((Layer)obj, superclassContent);

                case IRObjectType.IRComposition:
                    return FromLottieComposition((IRComposition)obj, superclassContent);

                case IRObjectType.Marker:
                    return FromMarker((Marker)obj, superclassContent);

                case IRObjectType.ShapeLayerContent:
                    return FromShapeLayerContent((ShapeLayerContent)obj, superclassContent);

                default:
                    throw Unreachable;
            }
        }

        YamlObject FromLottieComposition(IRComposition lottieComposition, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(lottieComposition.Version), Scalar(lottieComposition.Version));
            result.Add(nameof(lottieComposition.Width), lottieComposition.Width);
            result.Add(nameof(lottieComposition.Height), lottieComposition.Height);
            result.Add(nameof(lottieComposition.FramesPerSecond), lottieComposition.FramesPerSecond);
            result.Add(nameof(lottieComposition.InPoint), lottieComposition.InPoint);
            result.Add(nameof(lottieComposition.OutPoint), lottieComposition.OutPoint);
            result.Add(nameof(lottieComposition.Duration), lottieComposition.Duration);
            result.Add(nameof(lottieComposition.Assets), FromEnumerable(lottieComposition.Assets, FromAsset));
            result.Add(nameof(lottieComposition.Layers), FromLayerCollection(lottieComposition.Layers));
            result.Add(nameof(lottieComposition.Markers), FromEnumerable(lottieComposition.Markers, FromMarker));
            return result;
        }

        YamlSequence FromSequence<T>(Sequence<T> collection, Func<T, YamlObject> selector)
        {
            var result = new YamlSequence();
            foreach (var item in collection)
            {
                result.Add(selector(item));
            }

            return result;
        }

        YamlSequence FromEnumerable<T>(IEnumerable<T> collection, Func<T, YamlObject> selector)
        {
            var result = new YamlSequence();
            foreach (var item in collection)
            {
                result.Add(selector(item));
            }

            return result;
        }

        YamlObject FromAsset(Asset asset)
        {
            var superclassContent = new YamlMap
            {
                { nameof(asset.Id), asset.Id },
            };

            return asset.Type switch
            {
                Asset.AssetType.LayerCollection => FromLayersAsset((LayerCollectionAsset)asset, superclassContent),
                Asset.AssetType.Image => FromImageAsset((ImageAsset)asset, superclassContent),
                _ => throw Unreachable,
            };
        }

        YamlObject FromLayersAsset(LayerCollectionAsset asset, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(asset.Layers), FromLayerCollection(asset.Layers));
            return result;
        }

        YamlObject FromImageAsset(ImageAsset asset, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(asset.Width), asset.Width);
            superclassContent.Add(nameof(asset.Height), asset.Height);

            return asset.ImageType switch
            {
                ImageAsset.ImageAssetType.Embedded => FromEmbeddedImageAsset((EmbeddedImageAsset)asset, superclassContent),
                ImageAsset.ImageAssetType.External => FromExternalImageAsset((ExternalImageAsset)asset, superclassContent),
                _ => throw Unreachable,
            };
        }

        YamlObject FromEmbeddedImageAsset(EmbeddedImageAsset asset, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(asset.Format), asset.Format);
            result.Add(nameof(asset.Bytes.Length), asset.Bytes.Length);
            return result;
        }

        YamlObject FromExternalImageAsset(ExternalImageAsset asset, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(asset.Path), asset.Path);
            result.Add(nameof(asset.FileName), asset.FileName);
            return result;
        }

        YamlSequence FromLayerCollection(LayerCollection layers) =>
            FromEnumerable(layers.GetLayersBottomToTop().Reverse(), FromLayer);

        YamlObject FromLayer(Layer layer) => FromLayer(layer, GetLottieObjectContent(layer));

        YamlObject FromLayer(Layer layer, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(layer.Type), Scalar(layer.Type));
            superclassContent.Add(nameof(layer.Parent), layer.Parent);
            superclassContent.Add(nameof(layer.Index), layer.Index);
            superclassContent.Add(nameof(layer.IsHidden), layer.IsHidden);
            superclassContent.Add(nameof(layer.StartTime), layer.StartTime);
            superclassContent.Add(nameof(layer.InPoint), layer.InPoint);
            superclassContent.Add(nameof(layer.OutPoint), layer.OutPoint);
            superclassContent.Add(nameof(layer.TimeStretch), layer.TimeStretch);
            superclassContent.Add(nameof(layer.Transform), FromShapeLayerContent(layer.Transform));
            superclassContent.Add(nameof(layer.Effects), FromEnumerable(layer.Effects, FromEffect));
            superclassContent.Add(nameof(layer.Masks), FromEnumerable(layer.Masks, FromMask));
            superclassContent.Add(nameof(layer.LayerMatteType), Scalar(layer.LayerMatteType));

            return layer.Type switch
            {
                Layer.LayerType.PreComp => FromPreCompLayer((PreCompLayer)layer, superclassContent),
                Layer.LayerType.Solid => FromSolidLayer((SolidLayer)layer, superclassContent),
                Layer.LayerType.Image => FromImageLayer((ImageLayer)layer, superclassContent),
                Layer.LayerType.Null => FromNullLayer((NullLayer)layer, superclassContent),
                Layer.LayerType.Shape => FromShapeLayer((ShapeLayer)layer, superclassContent),
                Layer.LayerType.Text => FromTextLayer((TextLayer)layer, superclassContent),
                _ => throw Unreachable,
            };
        }

        YamlObject FromPreCompLayer(PreCompLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(layer.Width), layer.Width);
            result.Add(nameof(layer.Height), layer.Height);
            result.Add(nameof(layer.RefId), layer.RefId);
            return result;
        }

        YamlObject FromSolidLayer(SolidLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(layer.Width), layer.Width);
            result.Add(nameof(layer.Height), layer.Height);
            result.Add(nameof(layer.Color), Scalar(layer.Color));
            return result;
        }

        YamlObject FromImageLayer(ImageLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(layer.RefId), layer.RefId);
            return result;
        }

        YamlObject FromNullLayer(NullLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromShapeLayer(ShapeLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(layer.Contents), FromEnumerable(layer.Contents, FromShapeLayerContent));
            return result;
        }

        YamlObject FromTextLayer(TextLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromShapeLayerContent(ShapeLayerContent content) => FromShapeLayerContent(content, GetLottieObjectContent(content));

        YamlObject FromShapeLayerContent(ShapeLayerContent content, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(content.ContentType), Scalar(content.ContentType));

            switch (content.ContentType)
            {
                case ShapeContentType.Group:
                    return FromShapeGroup((ShapeGroup)content, superclassContent);
                case ShapeContentType.LinearGradientStroke:
                case ShapeContentType.RadialGradientStroke:
                case ShapeContentType.SolidColorStroke:
                    return FromShapeStroke((ShapeStroke)content, superclassContent);
                case ShapeContentType.LinearGradientFill:
                case ShapeContentType.RadialGradientFill:
                case ShapeContentType.SolidColorFill:
                    return FromShapeFill((ShapeFill)content, superclassContent);
                case ShapeContentType.Transform:
                    return FromTransform((Transform)content, superclassContent);
                case ShapeContentType.Ellipse:
                case ShapeContentType.Path:
                case ShapeContentType.Polystar:
                case ShapeContentType.Rectangle:
                    return FromShape((Shape)content, superclassContent);
                case ShapeContentType.TrimPath:
                    return FromTrimPath((TrimPath)content, superclassContent);
                case ShapeContentType.MergePaths:
                    return FromMergePaths((MergePaths)content, superclassContent);
                case ShapeContentType.Repeater:
                    return FromRepeater((Repeater)content, superclassContent);
                case ShapeContentType.RoundCorners:
                    return FromRoundCorners((RoundCorners)content, superclassContent);
                default:
                    throw Unreachable;
            }
        }

        YamlObject FromShape(Shape content, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(content.DrawingDirection), Scalar(content.DrawingDirection));

            return content.ContentType switch
            {
                ShapeContentType.Path => FromPath((Path)content, superclassContent),
                ShapeContentType.Ellipse => FromEllipse((Ellipse)content, superclassContent),
                ShapeContentType.Rectangle => FromRectangle((Rectangle)content, superclassContent),
                ShapeContentType.Polystar => FromPolystar((Polystar)content, superclassContent),
                _ => throw Unreachable,
            };
        }

        YamlObject FromShapeFill(ShapeFill content, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(content.Opacity), FromAnimatable(content.Opacity));
            superclassContent.Add(nameof(content.FillType), Scalar(content.FillType));
            return content.FillKind switch
            {
                ShapeFill.ShapeFillKind.SolidColor => FromSolidColorFill((SolidColorFill)content, superclassContent),
                ShapeFill.ShapeFillKind.LinearGradient => FromLinearGradientFill((LinearGradientFill)content, superclassContent),
                ShapeFill.ShapeFillKind.RadialGradient => FromRadialGradientFill((RadialGradientFill)content, superclassContent),
                _ => throw Unreachable,
            };
        }

        YamlObject FromShapeStroke(ShapeStroke content, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(content.Opacity), FromAnimatable(content.Opacity));
            superclassContent.Add(nameof(content.StrokeWidth), FromAnimatable(content.StrokeWidth));
            superclassContent.Add(nameof(content.CapType), Scalar(content.CapType));
            superclassContent.Add(nameof(content.JoinType), Scalar(content.JoinType));
            superclassContent.Add(nameof(content.MiterLimit), Scalar(content.MiterLimit));
            return content.StrokeKind switch
            {
                ShapeStroke.ShapeStrokeKind.SolidColor => FromSolidColorStroke((SolidColorStroke)content, superclassContent),
                ShapeStroke.ShapeStrokeKind.LinearGradient => FromLinearGradientStroke((LinearGradientStroke)content, superclassContent),
                ShapeStroke.ShapeStrokeKind.RadialGradient => FromRadialGradientStroke((RadialGradientStroke)content, superclassContent),
                _ => throw Unreachable,
            };
        }

        YamlObject FromEffect(Effect effect) =>
            FromEffect(effect, GetLottieObjectContent(effect));

        YamlObject FromEffect(Effect effect, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(effect.Type), effect.Type.ToString());
            superclassContent.Add(nameof(effect.IsEnabled), effect.IsEnabled);
            return effect.Type switch
            {
                Effect.EffectType.DropShadow => FromDropShadowEffect((DropShadowEffect)effect, superclassContent),
                Effect.EffectType.GaussianBlur => FromGaussianBlurEffect((GaussianBlurEffect)effect, superclassContent),

                // Handle all unknown effect types.
                _ => superclassContent,
            };
        }

        YamlObject FromDropShadowEffect(DropShadowEffect effect, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(effect.Color), FromAnimatable(effect.Color));
            result.Add(nameof(effect.Direction), FromAnimatable(effect.Direction));
            result.Add(nameof(effect.Distance), FromAnimatable(effect.Distance));
            result.Add(nameof(effect.IsShadowOnly), FromAnimatable(effect.IsShadowOnly));
            result.Add(nameof(effect.Opacity), FromAnimatable(effect.Opacity));
            result.Add(nameof(effect.Softness), FromAnimatable(effect.Softness));
            return result;
        }

        YamlObject FromGaussianBlurEffect(GaussianBlurEffect effect, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(effect.BlurDimensions), FromAnimatable(effect.BlurDimensions));
            result.Add(nameof(effect.Blurriness), FromAnimatable(effect.Blurriness));
            result.Add(nameof(effect.RepeatEdgePixels), FromAnimatable(effect.RepeatEdgePixels));
            result.Add(nameof(effect.ForceGpuRendering), effect.ForceGpuRendering);
            return result;
        }

        YamlObject FromMask(Mask mask)
            => new YamlMap
            {
                { nameof(mask.Name), mask.Name },
                { nameof(mask.Inverted), mask.Inverted },
                { nameof(mask.Mode), Scalar(mask.Mode) },
                { nameof(mask.Opacity), FromAnimatable(mask.Opacity) },
                { nameof(mask.Points), FromAnimatable(mask.Points, p => FromPathGeometry(p)) },
            };

        YamlObject FromShapeGroup(ShapeGroup content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Contents), FromEnumerable(content.Contents, FromShapeLayerContent));
            return result;
        }

        YamlObject FromSolidColorStroke(SolidColorStroke content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Color), FromAnimatable(content.Color));
            return result;
        }

        YamlObject FromLinearGradientStroke(LinearGradientStroke content, YamlMap superclassContent)
        {
            var result = superclassContent;
            AddFromIGradient(content, result);
            return result;
        }

        YamlObject FromRadialGradientStroke(RadialGradientStroke content, YamlMap superclassContent)
        {
            var result = superclassContent;
            AddFromIRadialGradient(content, result);
            return result;
        }

        YamlObject FromSolidColorFill(SolidColorFill content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Color), FromAnimatable(content.Color));
            return result;
        }

        YamlObject FromLinearGradientFill(LinearGradientFill content, YamlMap superclassContent)
        {
            var result = superclassContent;
            AddFromIGradient(content, result);
            return result;
        }

        YamlObject FromRadialGradientFill(RadialGradientFill content, YamlMap superclassContent)
        {
            var result = superclassContent;
            AddFromIRadialGradient(content, result);
            return result;
        }

        YamlObject FromTransform(Transform content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.ScalePercent), FromAnimatable(content.ScalePercent));
            result.Add(nameof(content.Position), FromAnimatable(content.Position));
            result.Add(nameof(content.Anchor), FromAnimatable(content.Anchor));
            result.Add(nameof(content.Opacity), FromAnimatable(content.Opacity));
            result.Add(nameof(content.Rotation), FromAnimatable(content.Rotation));
            return result;
        }

        YamlObject FromAnimatable(IAnimatableVector3 animatable)
        {
            switch (animatable.Type)
            {
                case AnimatableVector3Type.Vector3:
                    return FromAnimatable<Vector3>((AnimatableVector3)animatable, FromVector3);
                case AnimatableVector3Type.XYZ:
                    {
                        var xyz = (AnimatableXYZ)animatable;
                        var result = new YamlMap
                        {
                            { nameof(xyz.X), FromAnimatable(xyz.X) },
                            { nameof(xyz.Y), FromAnimatable(xyz.Y) },
                            { nameof(xyz.Z), FromAnimatable(xyz.Z) },
                        };
                        return result;
                    }

                default:
                    throw Unreachable;
            }
        }

        YamlObject FromAnimatable<T>(Animatable<T> animatable, Func<T, YamlObject> valueSelector)
            where T : IEquatable<T>
            => animatable.IsAnimated
                ? FromEnumerable(animatable.KeyFrames, kf => FromKeyFrame(kf, valueSelector))
                : valueSelector(animatable.InitialValue);

        YamlObject FromAnimatable(Animatable<bool> animatable) => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable(Animatable<Color> animatable) => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable(Animatable<double> animatable) => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable<T>(Animatable<Enum<T>> animatable)
            where T : struct, IComparable => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable(Animatable<Opacity> animatable) => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable(Animatable<Rotation> animatable) => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable(Animatable<Trim> animatable) => FromAnimatable(animatable, Scalar);

        static YamlObject FromCubicBezier(CubicBezier value)
        {
            var result = new YamlMap
            {
                { nameof(value.ControlPoint1), FromVector2(value.ControlPoint1) },
                { nameof(value.ControlPoint2), FromVector2(value.ControlPoint2) },
            };
            return result;
        }

        static YamlObject FromVector3(Vector3 value)
        {
            var result = new YamlMap
            {
                { nameof(value.X), value.X },
                { nameof(value.Y), value.Y },
                { nameof(value.Z), value.Z },
            };
            return result;
        }

        static YamlObject FromVector2(Vector2 value)
        {
            var result = new YamlMap
            {
                { nameof(value.X), value.X },
                { nameof(value.Y), value.Y },
            };
            return result;
        }

        static YamlObject FromBezierSegment(BezierSegment value)
        {
            var result = new YamlMap
            {
                { nameof(value.ControlPoint0), FromVector2(value.ControlPoint0) },
                { nameof(value.ControlPoint1), FromVector2(value.ControlPoint1) },
                { nameof(value.ControlPoint2), FromVector2(value.ControlPoint2) },
                { nameof(value.ControlPoint3), FromVector2(value.ControlPoint3) },
            };
            return result;
        }

        YamlObject FromGradientStop(GradientStop value) =>
            value.Kind switch
            {
                GradientStop.GradientStopKind.Color => FromColorGradientStop((ColorGradientStop)value),
                GradientStop.GradientStopKind.Opacity => FromOpacityGradientStop((OpacityGradientStop)value),
                _ => throw Unreachable,
            };

        YamlObject FromColorGradientStop(ColorGradientStop value)
        {
            var result = new YamlMap
            {
                { nameof(value.Color), Scalar(value.Color) },
                { nameof(value.Offset), Scalar(value.Offset) },
            };
            return result;
        }

        YamlObject FromOpacityGradientStop(OpacityGradientStop value)
        {
            var result = new YamlMap
            {
                { nameof(value.Opacity), Scalar(value.Opacity) },
                { nameof(value.Offset), Scalar(value.Offset) },
            };
            return result;
        }

        YamlMap FromKeyFrame<T>(KeyFrame<T> keyFrame, Func<T, YamlObject> valueSelector)
            where T : IEquatable<T>
        {
            var result = new YamlMap
            {
                { nameof(keyFrame.Frame), keyFrame.Frame },
                { nameof(keyFrame.Value), valueSelector(keyFrame.Value) },
                { nameof(keyFrame.Easing), FromEasing(keyFrame.Easing) },
            };

            if (keyFrame is KeyFrame<Vector3> v3kf)
            {
                if (v3kf.SpatialBezier?.IsLinear == false)
                {
                    // Spatial Bezier
                    result.Add(nameof(v3kf.SpatialBezier), FromCubicBezier(v3kf.SpatialBezier.Value));
                }
            }

            return result;
        }

        YamlObject FromEasing(Easing value)
        {
            var result = new YamlMap
            {
                { nameof(value.Type), Scalar(value.Type) },
            };

            return value.Type switch
            {
                // CubicBezierEasing is the only easing that has parameters.
                Easing.EasingType.CubicBezier => FromCubicBezierEasing((CubicBezierEasing)value, result),
                _ => result,
            };
        }

        YamlObject FromCubicBezierEasing(CubicBezierEasing content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Beziers), FromEnumerable(content.Beziers, FromCubicBezier));
            return result;
        }

        YamlObject FromPath(Path content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Data), FromAnimatable(content.Data, p => FromPathGeometry(p)));
            return result;
        }

        YamlObject FromPathGeometry(PathGeometry content)
        {
            var result = new YamlMap()
            {
                { nameof(content.IsClosed), content.IsClosed },
                { nameof(content.BezierSegments), FromSequence(content.BezierSegments, FromBezierSegment) },
            };

            return result;
        }

        YamlMap FromEllipse(Ellipse content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Diameter), FromAnimatable(content.Diameter));
            result.Add(nameof(content.Position), FromAnimatable(content.Position));
            return result;
        }

        YamlObject FromRectangle(Rectangle content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Size), FromAnimatable(content.Size));
            result.Add(nameof(content.Position), FromAnimatable(content.Position));
            result.Add(nameof(content.Roundness), FromAnimatable(content.Roundness));
            return result;
        }

        YamlObject FromPolystar(Polystar content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromTrimPath(TrimPath content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Start), FromAnimatable(content.Start));
            result.Add(nameof(content.End), FromAnimatable(content.End));
            result.Add(nameof(content.Offset), FromAnimatable(content.Offset));
            return result;
        }

        YamlObject FromMarker(Marker obj) => FromMarker(obj, GetLottieObjectContent(obj));

        YamlObject FromMarker(Marker obj, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(obj.Frame), obj.Frame);
            result.Add(nameof(obj.DurationInFrames), obj.DurationInFrames);
            return result;
        }

        YamlObject FromMergePaths(MergePaths content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromRoundCorners(RoundCorners content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(content.Radius), FromAnimatable(content.Radius));
            return result;
        }

        YamlObject FromRepeater(Repeater content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlScalar Scalar(bool value) => value;

        YamlScalar Scalar(Color value) => Scalar(value, $"'{value}'");

        YamlScalar Scalar(double value) => value;

        YamlScalar Scalar(Enum type) => Scalar(type, type.ToString());

        YamlObject Scalar<T>(Enum<T> value)
            where T : struct, IComparable => Scalar(value, value.ToString()!);

        YamlScalar Scalar(Opacity value) => Scalar(value, $"{value.Percent}%");

        YamlScalar Scalar(Rotation value) => Scalar(value, $"{value.Degrees}°");

        YamlScalar Scalar(Trim value) => Scalar(value, $"{value.Percent}%");

        YamlScalar Scalar(Version value) => Scalar(value, value.ToString());

        // The code we hit is supposed to be unreachable. This indicates a bug.
        static Exception Unreachable => new InvalidOperationException("Unreachable code executed");
    }
}
