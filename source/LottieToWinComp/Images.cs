// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates images.
    /// </summary>
    static class Images
    {
        public static LayerTranslator CreateImageLayerTranslator(ImageLayerContext context)
        {
            if (!Transforms.TryCreateContainerVisualTransformChain(context, out var containerVisualRootNode, out var containerVisualContentNode))
            {
                // The layer is never visible.
                return null;
            }

            var imageAsset = GetImageAsset(context);
            if (imageAsset is null)
            {
                return null;
            }

            var content = context.ObjectFactory.CreateSpriteVisual();
            containerVisualContentNode.Children.Add(content);
            content.Size = new Sn.Vector2((float)imageAsset.Width, (float)imageAsset.Height);

            LoadedImageSurface surface;
            var imageSize = $"{imageAsset.Width}x{imageAsset.Height}";

            switch (imageAsset.ImageType)
            {
                case ImageAsset.ImageAssetType.Embedded:
                    var embeddedImageAsset = (EmbeddedImageAsset)imageAsset;
                    surface = LoadedImageSurface.StartLoadFromStream(embeddedImageAsset.Bytes);
                    surface.SetName(imageAsset.Id);
                    surface.SetDescription(context, $"Image: \"{embeddedImageAsset.Id}\" {embeddedImageAsset.Format} {imageSize}.");
                    break;
                case ImageAsset.ImageAssetType.External:
                    var externalImageAsset = (ExternalImageAsset)imageAsset;
                    surface = LoadedImageSurface.StartLoadFromUri(new Uri($"file://localhost/{externalImageAsset.Path}{externalImageAsset.FileName}"));
                    surface.SetName(externalImageAsset.FileName);
                    var path = externalImageAsset.Path + externalImageAsset.FileName;
                    surface.SetDescription(context, $"\"{path}\" {imageSize}.");
                    context.Issues.ImageFileRequired(path);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            var imageBrush = context.ObjectFactory.CreateSurfaceBrush(surface);
            content.Brush = imageBrush;

            return new LayerTranslator.FromVisual(containerVisualRootNode);
        }

        static ImageAsset GetImageAsset(ImageLayerContext context) =>
            (ImageAsset)context.Translation.GetAssetById(context, context.Layer.RefId, Asset.AssetType.Image);
    }
}
