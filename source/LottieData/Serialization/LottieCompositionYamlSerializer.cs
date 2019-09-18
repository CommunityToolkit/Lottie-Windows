// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        public static void WriteYaml(LottieComposition root, TextWriter writer, string comment = null)
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
            var result = new YamlMap
            {
                { "Name", obj.Name },
            };
            return result;
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
                case LottieObjectType.RoundedCorner:
                case LottieObjectType.Shape:
                case LottieObjectType.ShapeGroup:
                case LottieObjectType.SolidColorFill:
                case LottieObjectType.SolidColorStroke:
                case LottieObjectType.Transform:
                case LottieObjectType.TrimPath:
                    return FromShapeLayerContent((ShapeLayerContent)obj, superclassContent);

                default:
                    throw new InvalidOperationException();
            }
        }

        YamlObject FromLottieComposition(LottieComposition lottieComposition, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Version", lottieComposition.Version);
            result.Add("Width", lottieComposition.Width);
            result.Add("Height", lottieComposition.Height);
            result.Add("InPoint", lottieComposition.InPoint);
            result.Add("OutPoint", lottieComposition.OutPoint);
            result.Add("Duration", lottieComposition.Duration);
            result.Add("Assets", FromEnumerable(lottieComposition.Assets, FromAsset));
            result.Add("Layers", FromLayerCollection(lottieComposition.Layers));
            result.Add("Markers", FromEnumerable(lottieComposition.Markers, FromMarker));
            return result;
        }

        YamlSequence FromSequence<T>(Sequence<T> collection, Func<T, YamlObject> selector)
        {
            var result = new YamlSequence();
            foreach (var item in collection.Items)
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

        YamlSequence FromSpan<T>(ReadOnlySpan<T> collection, Func<T, YamlObject> selector)
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
            switch (asset.Type)
            {
                case Asset.AssetType.LayerCollection:
                    return FromLayersAsset((LayerCollectionAsset)asset);
                case Asset.AssetType.Image:
                    return FromImageAsset((ImageAsset)asset);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlObject FromLayersAsset(LayerCollectionAsset asset)
        {
            var result = new YamlMap
            {
                { "Id", asset.Id },
                { "Layers", FromLayerCollection(asset.Layers) },
            };
            return result;
        }

        YamlObject FromImageAsset(ImageAsset asset)
        {
            switch (asset.ImageType)
            {
                case ImageAsset.ImageAssetType.Embedded:
                    return FromEmbeddedImageAsset((EmbeddedImageAsset)asset);
                case ImageAsset.ImageAssetType.External:
                    return FromExternalImageAsset((ExternalImageAsset)asset);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlObject FromEmbeddedImageAsset(EmbeddedImageAsset asset)
        {
            return new YamlMap
            {
                { "Id", asset.Id },
                { "Width", asset.Width },
                { "Height", asset.Height },
                { "Format", asset.Format},
                { "SizeInBytes", asset.Bytes.Length},
            };
        }

        YamlObject FromExternalImageAsset(ExternalImageAsset asset)
        {
            return new YamlMap
            {
                { "Id", asset.Id },
                { "Width", asset.Width },
                { "Height", asset.Height },
                { "Path", asset.Path },
                { "Filename", asset.FileName },
            };
        }

        YamlSequence FromLayerCollection(LayerCollection layers) =>
            FromEnumerable(layers.GetLayersBottomToTop().Reverse(), FromLayer);

        YamlObject FromLayer(Layer layer) => FromLayer(layer, GetLottieObjectContent(layer));

        YamlObject FromLayer(Layer layer, YamlMap superclassContent)
        {
            superclassContent.Add("Type", Scalar(layer.Type));
            superclassContent.Add("Parent", layer.Parent);
            superclassContent.Add("Index", layer.Index);
            superclassContent.Add("IsHidden", layer.IsHidden);
            superclassContent.Add("StartTime", layer.StartTime);
            superclassContent.Add("InPoint", layer.InPoint);
            superclassContent.Add("OutPoint", layer.OutPoint);
            superclassContent.Add("TimeStretch", layer.TimeStretch);
            superclassContent.Add("Transform", FromShapeLayerContent(layer.Transform));
            superclassContent.Add("Masks", FromSpan(layer.Masks, FromMask));
            superclassContent.Add("MatteType", Scalar(layer.LayerMatteType));

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
                    throw new InvalidOperationException();
            }
        }

        YamlObject FromPreCompLayer(PreCompLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Width", layer.Width);
            result.Add("Height", layer.Height);
            result.Add("RefId", layer.RefId);
            return result;
        }

        YamlObject FromSolidLayer(SolidLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Width", layer.Width);
            result.Add("Height", layer.Height);
            result.Add("Color", Scalar(layer.Color));
            return result;
        }

        YamlObject FromImageLayer(ImageLayer layer, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("RefId", layer.RefId);
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
            result.Add("Content", FromSpan(layer.Contents, FromShapeLayerContent));
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
            superclassContent.Add("MatchName", content.MatchName);
            superclassContent.Add("ContentType", Scalar(content.ContentType));

            switch (content.ContentType)
            {
                case ShapeContentType.Group:
                    return FromShapeGroup((ShapeGroup)content, superclassContent);
                case ShapeContentType.SolidColorStroke:
                    return FromSolidColorStroke((SolidColorStroke)content, superclassContent);
                case ShapeContentType.LinearGradientStroke:
                    return FromLinearGradientStroke((LinearGradientStroke)content, superclassContent);
                case ShapeContentType.RadialGradientStroke:
                    return FromRadialGradientStroke((RadialGradientStroke)content, superclassContent);
                case ShapeContentType.SolidColorFill:
                    return FromSolidColorFill((SolidColorFill)content, superclassContent);
                case ShapeContentType.LinearGradientFill:
                    return FromLinearGradientFill((LinearGradientFill)content, superclassContent);
                case ShapeContentType.RadialGradientFill:
                    return FromRadialGradientFill((RadialGradientFill)content, superclassContent);
                case ShapeContentType.Transform:
                    return FromTransform((Transform)content, superclassContent);
                case ShapeContentType.Path:
                    return FromPath((Path)content, superclassContent);
                case ShapeContentType.Ellipse:
                    return FromEllipse((Ellipse)content, superclassContent);
                case ShapeContentType.Rectangle:
                    return FromRectangle((Rectangle)content, superclassContent);
                case ShapeContentType.Polystar:
                    return FromPolystar((Polystar)content, superclassContent);
                case ShapeContentType.TrimPath:
                    return FromTrimPath((TrimPath)content, superclassContent);
                case ShapeContentType.MergePaths:
                    return FromMergePaths((MergePaths)content, superclassContent);
                case ShapeContentType.Repeater:
                    return FromRepeater((Repeater)content, superclassContent);
                case ShapeContentType.RoundedCorner:
                    return FromRoundedCorner((RoundedCorner)content, superclassContent);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlObject FromMask(Mask mask)
        {
            var result = new YamlMap
            {
                { "Name", mask.Name },
                { "Inverted", mask.Inverted },
                { "Mode", Scalar(mask.Mode) },
                { "OpacityPercent", FromAnimatable(mask.OpacityPercent) },
                { "Points", FromAnimatable(mask.Points, p => FromSequence(p, FromBezierSegment)) },
            };
            return result;
        }

        YamlObject FromShapeGroup(ShapeGroup content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Items", FromSpan(content.Contents, FromShapeLayerContent));
            return result;
        }

        YamlObject FromSolidColorStroke(SolidColorStroke content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Color", FromAnimatable(content.Color, FromColor));
            result.Add("OpacityPercent", FromAnimatable(content.OpacityPercent));
            result.Add("Thickness", FromAnimatable(content.Thickness));
            return result;
        }

        YamlObject FromLinearGradientStroke(LinearGradientStroke content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromRadialGradientStroke(RadialGradientStroke content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromSolidColorFill(SolidColorFill content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Color", FromAnimatable(content.Color, FromColor));
            result.Add("OpacityPercent", FromAnimatable(content.OpacityPercent));
            return result;
        }

        YamlObject FromLinearGradientFill(LinearGradientFill content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("OpacityPercent", FromAnimatable(content.OpacityPercent));
            result.Add("ColorStops", FromAnimatable(content.ColorStops, p => FromSequence(p, FromColorStop)));
            result.Add("OpacityPercentStops", FromAnimatable(content.OpacityPercentStops, p => FromSequence(p, FromOpacityPercentStop)));
            return result;
        }

        YamlObject FromRadialGradientFill(RadialGradientFill content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("OpacityPercent", FromAnimatable(content.OpacityPercent));
            result.Add("ColorStops", FromAnimatable(content.ColorStops, p => FromSequence(p, FromColorStop)));
            result.Add("OpacityPercentStops", FromAnimatable(content.OpacityPercentStops, p => FromSequence(p, FromOpacityPercentStop)));
            return result;
        }

        YamlObject FromTransform(Transform content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("ScalePercent", FromAnimatable(content.ScalePercent));
            result.Add("Position", FromAnimatable(content.Position));
            result.Add("Anchor", FromAnimatable(content.Anchor));
            result.Add("OpacityPercent", FromAnimatable(content.OpacityPercent));
            result.Add("RotationDegrees", FromAnimatable(content.RotationDegrees));
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
                            { "X", FromAnimatable(xyz.X) },
                            { "Y", FromAnimatable(xyz.Y) },
                            { "Z", FromAnimatable(xyz.Z) },
                        };
                        return result;
                    }

                default:
                    throw new InvalidOperationException();
            }
        }

        YamlObject FromAnimatable<T>(Animatable<T> animatable, Func<T, YamlObject> valueSelector)
            where T : IEquatable<T>
        {
            if (!animatable.IsAnimated)
            {
                return valueSelector(animatable.InitialValue);
            }
            else
            {
                return FromSpan<KeyFrame<T>>(animatable.KeyFrames, kf => FromKeyFrame(kf, valueSelector));
            }
        }

        YamlObject FromAnimatable(Animatable<double> animatable)
            => FromAnimatable(animatable, FromDouble);

        static YamlObject FromDouble(double value) => (YamlScalar)value;

        static YamlObject FromColor(Color value) => (YamlScalar)value?.ToString();

        static YamlObject FromVector3(Vector3 value)
        {
            var result = new YamlMap
            {
                { "X", value.X },
                { "Y", value.Y },
                { "Z", value.Z },
            };
            return result;
        }

        static YamlObject FromVector2(Vector2 value)
        {
            var result = new YamlMap
            {
                { "X", value.X },
                { "Y", value.Y },
            };
            return result;
        }

        static YamlObject FromBezierSegment(BezierSegment value)
        {
            var result = new YamlMap
            {
                { "ControlPoint0", FromVector2(value.ControlPoint0) },
                { "ControlPoint1", FromVector2(value.ControlPoint1) },
                { "ControlPoint2", FromVector2(value.ControlPoint2) },
                { "ControlPoint3", FromVector2(value.ControlPoint3) },
            };
            return result;
        }

        static YamlObject FromColorStop(ColorGradientStop value)
        {
            var result = new YamlMap
            {
                { "Color", FromColor(value.Color) },
                { "Offset", value.Offset},
            };
            return result;
        }

        static YamlObject FromOpacityPercentStop(OpacityGradientStop value)
        {
            var result = new YamlMap
            {
                { "OpacityPercent", value.OpacityPercent },
                { "Offset", value.Offset },
            };
            return result;
        }

        YamlMap FromKeyFrame<T>(KeyFrame<T> keyFrame, Func<T, YamlObject> valueSelector)
            where T : IEquatable<T>
        {
            var result = new YamlMap
            {
                { "Frame", keyFrame.Frame },
                { "Value", valueSelector(keyFrame.Value) },
                { "Easing", Scalar(keyFrame.Easing.Type) },
            };

            if (keyFrame is KeyFrame<Vector3>)
            {
                var v3kf = (KeyFrame<Vector3>)(object)keyFrame;
                var cp1 = v3kf.SpatialControlPoint1;
                var cp2 = v3kf.SpatialControlPoint2;
                if (cp1 != Vector3.Zero || cp2 != Vector3.Zero)
                {
                    // Spatial bezier
                    result.Add("ControlPoint1", FromVector3(cp1));
                    result.Add("ControlPoint2", FromVector3(cp2));
                }
            }

            return result;
        }

        YamlObject FromPath(Path content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Direction", content.Direction);
            result.Add("Data", FromAnimatable(content.Data, p => FromSequence(p, FromBezierSegment)));
            return result;
        }

        YamlMap FromEllipse(Ellipse content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Diameter", FromAnimatable(content.Diameter));
            result.Add("Position", FromAnimatable(content.Position));
            return result;
        }

        YamlObject FromRectangle(Rectangle content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Size", FromAnimatable(content.Size));
            result.Add("Position", FromAnimatable(content.Position));
            result.Add("CornerRadius", FromAnimatable(content.CornerRadius));
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
            result.Add("StartPercent", FromAnimatable(content.StartPercent));
            result.Add("EndPercent", FromAnimatable(content.EndPercent));
            result.Add("OffsetDegrees", FromAnimatable(content.OffsetDegrees));
            return result;
        }

        YamlObject FromMarker(Marker obj) => FromMarker(obj, GetLottieObjectContent(obj));

        YamlObject FromMarker(Marker obj, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Progress", obj.Progress);
            return result;
        }

        YamlObject FromMergePaths(MergePaths content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlObject FromRoundedCorner(RoundedCorner content, YamlMap superclassContent)
        {
            var result = superclassContent;
            result.Add("Radius", FromAnimatable(content.Radius));
            return result;
        }

        YamlObject FromRepeater(Repeater content, YamlMap superclassContent)
        {
            var result = superclassContent;
            return result;
        }

        YamlScalar Scalar(Color value) => Scalar(value, $"'{value.ToString()}'");

        YamlScalar Scalar(Easing.EasingType type) => Scalar(type, type.ToString());

        YamlScalar Scalar(Layer.LayerType type) => Scalar(type, type.ToString());

        YamlScalar Scalar(Mask.MaskMode type) => Scalar(type, type.ToString());

        YamlScalar Scalar(ShapeContentType type) => Scalar(type, type.ToString());

        YamlScalar Scalar(Layer.MatteType type) => Scalar(type, type.ToString());
    }
}
