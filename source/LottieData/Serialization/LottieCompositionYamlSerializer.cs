// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.YamlData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#if PUBLIC_LottieData
    public
#endif
    sealed class LottieCompositionYamlSerializer : YamlFactory
    {
        public static void WriteYaml(LottieComposition root, TextWriter writer, string? comment = null)
        {
            var serializer = new LottieCompositionYamlSerializer();

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

        YamlMap GetLottieObjectContent(LottieObject obj)
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

        YamlObject FromLottieObject(LottieObject obj)
        {
            var superclassContent = GetLottieObjectContent(obj);

            switch (obj.ObjectType)
            {
                case LottieObjectType.LottieComposition:
                    return FromLottieComposition((LottieComposition)obj, superclassContent);

                case LottieObjectType.Marker:
                    return FromMarker((Marker)obj, superclassContent);

                case LottieObjectType.ImageLayer:
                case LottieObjectType.NullLayer:
                case LottieObjectType.PreCompLayer:
                case LottieObjectType.ShapeLayer:
                case LottieObjectType.SolidLayer:
                case LottieObjectType.TextLayer:
                    return FromLayer((Layer)obj, superclassContent);

                case LottieObjectType.Ellipse:
                case LottieObjectType.LinearGradientFill:
                case LottieObjectType.LinearGradientStroke:
                case LottieObjectType.MergePaths:
                case LottieObjectType.Polystar:
                case LottieObjectType.RadialGradientFill:
                case LottieObjectType.RadialGradientStroke:
                case LottieObjectType.Rectangle:
                case LottieObjectType.Repeater:
                case LottieObjectType.RoundCorners:
                case LottieObjectType.Shape:
                case LottieObjectType.ShapeGroup:
                case LottieObjectType.SolidColorFill:
                case LottieObjectType.SolidColorStroke:
                case LottieObjectType.Transform:
                case LottieObjectType.TrimPath:
                    return FromShapeLayerContent((ShapeLayerContent)obj, superclassContent);

                default:
                    throw Unreachable;
            }
        }

        YamlObject FromLottieComposition(LottieComposition lottieComposition, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add(nameof(lottieComposition.Version), lottieComposition.Version);
            result.Add(nameof(lottieComposition.Width), lottieComposition.Width);
            result.Add(nameof(lottieComposition.Height), lottieComposition.Height);
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

            switch (asset.Type)
            {
                case Asset.AssetType.LayerCollection:
                    return FromLayersAsset((LayerCollectionAsset)asset, superclassContent);
                case Asset.AssetType.Image:
                    return FromImageAsset((ImageAsset)asset, superclassContent);
                default:
                    throw Unreachable;
            }
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

            switch (asset.ImageType)
            {
                case ImageAsset.ImageAssetType.Embedded:
                    return FromEmbeddedImageAsset((EmbeddedImageAsset)asset, superclassContent);
                case ImageAsset.ImageAssetType.External:
                    return FromExternalImageAsset((ExternalImageAsset)asset, superclassContent);
                default:
                    throw Unreachable;
            }
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
            superclassContent.Add(nameof(layer.Masks), FromEnumerable(layer.Masks, FromMask));
            superclassContent.Add(nameof(layer.LayerMatteType), Scalar(layer.LayerMatteType));

            switch (layer.Type)
            {
                case Layer.LayerType.PreComp:
                    return FromPreCompLayer((PreCompLayer)layer, superclassContent);
                case Layer.LayerType.Solid:
                    return FromSolidLayer((SolidLayer)layer, superclassContent);
                case Layer.LayerType.Image:
                    return FromImageLayer((ImageLayer)layer, superclassContent);
                case Layer.LayerType.Null:
                    return FromNullLayer((NullLayer)layer, superclassContent);
                case Layer.LayerType.Shape:
                    return FromShapeLayer((ShapeLayer)layer, superclassContent);
                case Layer.LayerType.Text:
                    return FromTextLayer((TextLayer)layer, superclassContent);
                default:
                    throw Unreachable;
            }
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

            switch (content.ContentType)
            {
                case ShapeContentType.Path:
                    return FromPath((Path)content, superclassContent);
                case ShapeContentType.Ellipse:
                    return FromEllipse((Ellipse)content, superclassContent);
                case ShapeContentType.Rectangle:
                    return FromRectangle((Rectangle)content, superclassContent);
                case ShapeContentType.Polystar:
                    return FromPolystar((Polystar)content, superclassContent);
                default:
                    throw Unreachable;
            }
        }

        YamlObject FromShapeFill(ShapeFill content, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(content.Opacity), FromAnimatable(content.Opacity));
            superclassContent.Add(nameof(content.FillType), Scalar(content.FillType));
            switch (content.FillKind)
            {
                case ShapeFill.ShapeFillKind.SolidColor:
                    return FromSolidColorFill((SolidColorFill)content, superclassContent);
                case ShapeFill.ShapeFillKind.LinearGradient:
                    return FromLinearGradientFill((LinearGradientFill)content, superclassContent);
                case ShapeFill.ShapeFillKind.RadialGradient:
                    return FromRadialGradientFill((RadialGradientFill)content, superclassContent);
                default:
                    throw Unreachable;
            }
        }

        YamlObject FromShapeStroke(ShapeStroke content, YamlMap superclassContent)
        {
            superclassContent.Add(nameof(content.Opacity), FromAnimatable(content.Opacity));
            superclassContent.Add(nameof(content.StrokeWidth), FromAnimatable(content.StrokeWidth));
            superclassContent.Add(nameof(content.CapType), Scalar(content.CapType));
            superclassContent.Add(nameof(content.JoinType), Scalar(content.JoinType));
            superclassContent.Add(nameof(content.MiterLimit), Scalar(content.MiterLimit));
            switch (content.StrokeKind)
            {
                case ShapeStroke.ShapeStrokeKind.SolidColor:
                    return FromSolidColorStroke((SolidColorStroke)content, superclassContent);
                case ShapeStroke.ShapeStrokeKind.LinearGradient:
                    return FromLinearGradientStroke((LinearGradientStroke)content, superclassContent);
                case ShapeStroke.ShapeStrokeKind.RadialGradient:
                    return FromRadialGradientStroke((RadialGradientStroke)content, superclassContent);
                default:
                    throw Unreachable;
            }
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

        YamlObject FromAnimatable(Animatable<Color> animatable) => FromAnimatable(animatable, Scalar);

        YamlObject FromAnimatable(Animatable<double> animatable) => FromAnimatable(animatable, Scalar);

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

        YamlObject FromGradientStop(GradientStop value)
        {
            switch (value.Kind)
            {
                case GradientStop.GradientStopKind.Color:
                    return FromColorGradientStop((ColorGradientStop)value);
                case GradientStop.GradientStopKind.Opacity:
                    return FromOpacityGradientStop((OpacityGradientStop)value);
                default:
                    throw Unreachable;
            }
        }

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

            switch (value.Type)
            {
                case Easing.EasingType.CubicBezier:
                    // CubicBezierEasing is the only easing that has parameters.
                    return FromCubicBezierEasing((CubicBezierEasing)value, result);
                default:
                    return result;
            }
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

        YamlScalar Scalar(Color value) => Scalar(value, $"'{value}'");

        YamlScalar Scalar(double value) => value;

        YamlScalar Scalar(Enum type) => Scalar(type, type.ToString());

        YamlScalar Scalar(Mask.MaskMode type) => Scalar(type, type.ToString());

        YamlScalar Scalar(Opacity value) => Scalar(value, $"{value.Percent}%");

        YamlScalar Scalar(Rotation value) => Scalar(value, $"{value.Degrees}°");

        YamlScalar Scalar(Trim value) => Scalar(value, $"{value.Percent}%");

        // The code we hit is supposed to be unreachable. This indicates a bug.
        static Exception Unreachable => new InvalidOperationException("Unreachable code executed");
    }
}
