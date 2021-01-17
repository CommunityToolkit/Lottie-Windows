// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Sn = System.Numerics;

#if DEBUG
// For diagnosing issues, give nothing a clip.
//#define NoClipping
#endif

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    static class SolidLayers
    {
        public static LayerTranslator CreateSolidLayerTranslator(SolidLayerContext context)
        {
            // Emit issues for unupported layer effects.
            context.Effects.EmitIssueIfDropShadow();
            context.Effects.EmitIssueIfGaussianBlur();

            return new SolidLayerTranslator(context);
        }

        sealed class SolidLayerTranslator : LayerTranslator
        {
            readonly SolidLayerContext _context;

            internal SolidLayerTranslator(SolidLayerContext context)
            {
                _context = context;
            }

            internal override bool IsShape =>
                !_context.Layer.Masks.Any() || _context.Layer.IsHidden || _context.Layer.Transform.Opacity.IsAlways(Animatables.Opacity.Transparent);

            internal override CompositionShape? GetShapeRoot(TranslationContext context)
            {
                if (_context.Layer.IsHidden || _context.Layer.Transform.Opacity.IsAlways(Animatables.Opacity.Transparent))
                {
                    // The layer does not render anything. Nothing to translate. This can happen when someone
                    // creates a solid layer to act like a Null layer.
                    return null;
                }

                if (!Transforms.TryCreateContainerShapeTransformChain(_context, out var containerRootNode, out var containerContentNode))
                {
                    // The layer is never visible.
                    return null;
                }

                var rectangle = context.ObjectFactory.CreateSpriteShape();

                var rectangleGeometry = context.ObjectFactory.CreateRectangleGeometry();

                rectangleGeometry.Size = new Sn.Vector2(_context.Layer.Width, _context.Layer.Height);

                rectangle.Geometry = rectangleGeometry;

                containerContentNode.Shapes.Add(rectangle);

                // Opacity is implemented via the alpha channel on the brush.
                rectangle.FillBrush = Brushes.CreateAnimatedColorBrush(_context, _context.Layer.Color, Optimizer.TrimAnimatable(_context, _context.Layer.Transform.Opacity));

                rectangle.SetDescription(context, () => "SolidLayerRectangle");
                rectangle.Geometry.SetDescription(context, () => "SolidLayerRectangle.RectangleGeometry");
                Describe(context, containerRootNode);

                return containerRootNode;
            }

            internal override Visual? GetVisualRoot(CompositionContext context)
            {
                // Translate the SolidLayer to a Visual.
                if (_context.Layer.IsHidden || _context.Layer.Transform.Opacity.IsAlways(Animatables.Opacity.Transparent))
                {
                    // The layer does not render anything. Nothing to translate. This can happen when someone
                    // creates a solid layer to act like a Null layer.
                    return null;
                }

                if (!Transforms.TryCreateContainerVisualTransformChain(_context, out var containerRootNode, out var containerContentNode))
                {
                    // The layer is never visible.
                    return null;
                }

                var rectangle = context.ObjectFactory.CreateSpriteVisual();
                rectangle.Size = ConvertTo.Vector2(_context.Layer.Width, _context.Layer.Height);

                containerContentNode.Children.Add(rectangle);

                var layerHasMasks = false;
#if !NoClipping
                layerHasMasks = _context.Layer.Masks.Any();
#endif
                rectangle.Brush = Brushes.CreateNonAnimatedColorBrush(_context, _context.Layer.Color);

                rectangle.SetDescription(context, () => "SolidLayerRectangle");

                var result = layerHasMasks
                    ? Masks.TranslateAndApplyMasksForLayer(_context, containerRootNode)
                    : containerRootNode;

                Describe(context, result);

                return result;
            }
        }
    }
}
