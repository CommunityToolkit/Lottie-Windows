// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools
{
    /// <summary>
    /// Calculates stats for a <see cref="LottieComposition"/>.
    /// </summary>
#if PUBLIC_LottieData
    public
#endif
    sealed class Stats
    {
        // Initializes the Stats from a given LottieComposition.
        public Stats(LottieComposition lottieComposition)
        {
            Name = lottieComposition.Name;
            Version = lottieComposition.Version;
            Width = lottieComposition.Width;
            Height = lottieComposition.Height;
            Duration = lottieComposition.Duration;

            // Get the layers stored in assets.
            var layersInAssets =
                from asset in lottieComposition.Assets
                where asset.Type == Asset.AssetType.LayerCollection
                let layerCollection = (LayerCollectionAsset)asset
                from layer in layerCollection.Layers.GetLayersBottomToTop()
                select layer;

            foreach (var layer in lottieComposition.Layers.GetLayersBottomToTop().Concat(layersInAssets))
            {
                switch (layer.Type)
                {
                    case Layer.LayerType.PreComp:
                        PreCompLayerCount++;
                        break;
                    case Layer.LayerType.Solid:
                        SolidLayerCount++;
                        break;
                    case Layer.LayerType.Image:
                        ImageLayerCount++;
                        break;
                    case Layer.LayerType.Null:
                        NullLayerCount++;
                        break;
                    case Layer.LayerType.Shape:
                        ShapeLayerCount++;
                        VisitShapeLayer((ShapeLayer)layer);
                        break;
                    case Layer.LayerType.Text:
                        TextLayerCount++;
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                foreach (var mask in layer.Masks)
                {
                    MaskCount++;
                    switch (mask.Mode)
                    {
                        case Mask.MaskMode.Add:
                            MaskAddCount++;
                            break;
                        case Mask.MaskMode.Darken:
                            MaskDarkenCount++;
                            break;
                        case Mask.MaskMode.Difference:
                            MaskDifferenceCount++;
                            break;
                        case Mask.MaskMode.Intersect:
                            MaskIntersectCount++;
                            break;
                        case Mask.MaskMode.Lighten:
                            MaskLightenCount++;
                            break;
                        case Mask.MaskMode.Subtract:
                            MaskSubtractCount++;
                            break;
                    }
                }

                MaskCount += layer.Masks.Count;
            }
        }

        public int PreCompLayerCount { get; }

        public int SolidLayerCount { get; }

        public int ImageLayerCount { get; }

        public int LinearGradientFillCount { get; private set; }

        public int LinearGradientStrokeCount { get; private set; }

        public int MaskCount { get; }

        public int MaskAddCount { get; }

        public int MaskDarkenCount { get; }

        public int MaskDifferenceCount { get; }

        public int MaskIntersectCount { get; }

        public int MaskLightenCount { get; }

        public int MaskSubtractCount { get; }

        public int NullLayerCount { get; }

        public int RadialGradientFillCount { get; private set; }

        public int RadialGradientStrokeCount { get; private set; }

        public int ShapeLayerCount { get; }

        public int TextLayerCount { get; }

        public double Width { get; }

        public double Height { get; }

        public TimeSpan Duration { get; }

        public string Name { get; }

        public Version Version { get; }

        void VisitShapeLayer(ShapeLayer shapeLayer)
        {
            foreach (var content in shapeLayer.Contents)
            {
                VisitShapeLayerContent(content);
            }
        }

        void VisitShapeGroup(ShapeGroup shapeGroup)
        {
            foreach (var content in shapeGroup.Contents)
            {
                VisitShapeLayerContent(content);
            }
        }

        void VisitShapeLayerContent(ShapeLayerContent content)
        {
            switch (content.ContentType)
            {
                case ShapeContentType.Ellipse:
                    break;
                case ShapeContentType.Group:
                    VisitShapeGroup((ShapeGroup)content);
                    break;
                case ShapeContentType.LinearGradientFill:
                    LinearGradientFillCount++;
                    break;
                case ShapeContentType.LinearGradientStroke:
                    LinearGradientStrokeCount++;
                    break;
                case ShapeContentType.MergePaths:
                    break;
                case ShapeContentType.Path:
                    break;
                case ShapeContentType.Polystar:
                    break;
                case ShapeContentType.RadialGradientFill:
                    RadialGradientFillCount++;
                    break;
                case ShapeContentType.RadialGradientStroke:
                    RadialGradientStrokeCount++;
                    break;
                case ShapeContentType.Rectangle:
                    break;
                case ShapeContentType.Repeater:
                    break;
                case ShapeContentType.RoundCorners:
                    break;
                case ShapeContentType.SolidColorFill:
                    break;
                case ShapeContentType.SolidColorStroke:
                    break;
                case ShapeContentType.Transform:
                    break;
                case ShapeContentType.TrimPath:
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
