// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translation for Lottie shapes.
    /// </summary>
    static class Shapes
    {
        public static LayerTranslator CreateShapeLayerTranslator(ShapeLayerContext context)
        {
            return new ShapeLayerTranslator(context);
        }

        public static void TranslateAndApplyShapeContext(
            ShapeContext context,
            CompositionSpriteShape shape,
            bool reverseDirection) =>
            TranslateAndApplyShapeContextWithTrimOffset(context, shape, reverseDirection, 0);

        public static void TranslateAndApplyShapeContextWithTrimOffset(
            ShapeContext context,
            CompositionSpriteShape shape,
            bool reverseDirection,
            double trimOffsetDegrees)
        {
            Debug.Assert(shape.Geometry is not null, "Precondition");

            shape.FillBrush = Brushes.TranslateShapeFill(context, context.Fill, context.Opacity);

            // OriginOffset is used to adjust cordinates of FillBrush for Rectangle shapes.
            // It is not needed afterwards, so we clean it up to not affect other code.
            context.LayerContext.OriginOffset = null;

            Brushes.TranslateAndApplyStroke(context, context.Stroke, shape, context.Opacity);

            TranslateAndApplyTrimPath(
                context,
                geometry: shape.Geometry!,
                reverseDirection,
                trimOffsetDegrees);
        }

        static CompositionShape TranslateGroupShapeContent(ShapeContext context, ShapeGroup group)
        {
            var result = TranslateShapeLayerContents(context, group.Contents);
            result.SetDescription(context, () => $"ShapeGroup: {group.Name}");
            return result;
        }

        static CompositionShape TranslateShapeLayerContents(
            ShapeContext context,
            IReadOnlyList<ShapeLayerContent> contents)
        {
            // The Contents of a ShapeLayer is a list of instructions for a stack machine.

            // When evaluated, the stack of ShapeLayerContent produces a list of CompositionShape.
            // Some ShapeLayerContent modify the evaluation context (e.g. stroke, fill, trim)
            // Some ShapeLayerContent evaluate to geometries (e.g. any geometry, merge path)

            // Create a container to hold the contents.
            var container = context.ObjectFactory.CreateContainerShape();

            // This is the object that will be returned. Containers may be added above this
            // as necessary to hold transforms.
            var result = container;

            // If the contents contains a repeater, generate repeated contents
            if (contents.Any(slc => slc.ContentType == ShapeContentType.Repeater))
            {
                // The contents contains a repeater. Treat it as if there are n sets of items (where n
                // equals the Count of the repeater). In each set, replace the repeater with
                // the transform of the repeater, multiplied.

                // Find the index of the repeater
                var repeaterIndex = 0;
                while (contents[repeaterIndex].ContentType != ShapeContentType.Repeater)
                {
                    // Keep going until the first repeater is found.
                    repeaterIndex++;
                }

                // Get the repeater.
                var repeater = (Repeater)contents[repeaterIndex];

                var repeaterCount = Optimizer.TrimAnimatable(context, repeater.Count);
                var repeaterOffset = Optimizer.TrimAnimatable(context, repeater.Offset);

                // Make sure we can handle it.
                if (repeaterCount.IsAnimated || repeaterOffset.IsAnimated || repeaterOffset.InitialValue != 0)
                {
                    // TODO - handle all cases.
                    context.Issues.RepeaterIsNotSupported();
                }
                else
                {
                    // Get the items before the repeater, and the items after the repeater.
                    var itemsBeforeRepeater = contents.Slice(0, repeaterIndex).ToArray();
                    var itemsAfterRepeater = contents.Slice(repeaterIndex + 1).ToArray();

                    var nonAnimatedRepeaterCount = (int)Math.Round(repeaterCount.InitialValue);
                    for (var i = 0; i < nonAnimatedRepeaterCount; i++)
                    {
                        // Treat each repeated value as a list of items where the repeater is replaced
                        // by n transforms.
                        // TODO - currently ignoring the StartOpacity and EndOpacity - should generate a new transform
                        //        that interpolates that.
                        var generatedItems = itemsBeforeRepeater.Concat(Enumerable.Repeat(repeater.Transform, i + 1)).Concat(itemsAfterRepeater).ToArray();

                        // Recurse to translate the synthesized items.
                        container.Shapes.Add(TranslateShapeLayerContents(context, generatedItems));
                    }

                    return result;
                }
            }

            CheckForUnsupportedShapeGroup(context, contents);

            var stack = new Stack<ShapeLayerContent>(contents.ToArray());

            while (true)
            {
                context.UpdateFromStack(stack);
                if (stack.Count == 0)
                {
                    break;
                }

                var shapeContent = stack.Pop();

                // Complain if the BlendMode is not supported.
                if (shapeContent.BlendMode != BlendMode.Normal)
                {
                    context.Issues.BlendModeNotNormal(context.LayerContext.Layer.Name, shapeContent.BlendMode.ToString());
                }

                switch (shapeContent.ContentType)
                {
                    case ShapeContentType.Ellipse:
                        container.Shapes.Add(Ellipses.TranslateEllipseContent(context, (Ellipse)shapeContent));
                        break;
                    case ShapeContentType.Group:
                        container.Shapes.Add(TranslateGroupShapeContent(context.Clone(), (ShapeGroup)shapeContent));
                        break;
                    case ShapeContentType.MergePaths:
                        var mergedPaths = TranslateMergePathsContent(context, stack, ((MergePaths)shapeContent).Mode);
                        if (mergedPaths is not null)
                        {
                            container.Shapes.Add(mergedPaths);
                        }

                        break;
                    case ShapeContentType.Path:
                        {
                            var paths = new List<Path>();
                            paths.Add(Optimizer.OptimizePath(context, (Path)shapeContent));

                            // Get all the paths that are part of the same group.
                            while (stack.TryPeek(out var item) && item.ContentType == ShapeContentType.Path)
                            {
                                // Optimize the paths as they are added. Optimized paths have redundant keyframes
                                // removed. Optimizing here increases the chances that an animated path will be
                                // turned into a non-animated path which will allow us to group the paths.
                                paths.Add(Optimizer.OptimizePath(context, (Path)stack.Pop()));
                            }

                            if (paths.Count == 1)
                            {
                                // There's a single path.
                                container.Shapes.Add(Paths.TranslatePathContent(context, paths[0]));
                            }
                            else
                            {
                                // TODO: add support for round corners for multiple paths. I didn't find a way to generate
                                // AE animation with multiple paths on the same shape.
                                CheckForRoundCornersOnPath(context);

                                // There are multiple paths. They need to be grouped.
                                container.Shapes.Add(Paths.TranslatePathGroupContent(context, paths));
                            }
                        }

                        break;
                    case ShapeContentType.Polystar:
                        context.Issues.PolystarIsNotSupported();
                        break;
                    case ShapeContentType.Rectangle:
                        container.Shapes.Add(Rectangles.TranslateRectangleContent(context, (Rectangle)shapeContent));
                        break;
                    case ShapeContentType.Transform:
                        {
                            var transform = (Transform)shapeContent;

                            // Multiply the opacity in the transform.
                            context.UpdateOpacityFromTransform(context, transform);

                            // Insert a new container at the top. The transform will be applied to it.
                            var newContainer = context.ObjectFactory.CreateContainerShape();
                            newContainer.Shapes.Add(result);
                            result = newContainer;

                            // Apply the transform to the new container at the top.
                            Transforms.TranslateAndApplyTransform(context, transform, result);
                        }

                        break;
                    case ShapeContentType.Repeater:
                        // TODO - handle all cases. Not clear whether this is valid. Seen on 0605.traffic_light.
                        context.Issues.RepeaterIsNotSupported();
                        break;
                    default:
                    case ShapeContentType.SolidColorStroke:
                    case ShapeContentType.LinearGradientStroke:
                    case ShapeContentType.RadialGradientStroke:
                    case ShapeContentType.SolidColorFill:
                    case ShapeContentType.LinearGradientFill:
                    case ShapeContentType.RadialGradientFill:
                    case ShapeContentType.TrimPath:
                    case ShapeContentType.RoundCorners:
                        throw new InvalidOperationException();
                }
            }

            return result;
        }

        // Merge the stack into a single shape. Merging is done recursively - the top geometry on the
        // stack is merged with the merge of the remainder of the stack.
        static CompositionShape? TranslateMergePathsContent(ShapeContext context, Stack<ShapeLayerContent> stack, MergePaths.MergeMode mergeMode)
        {
            var mergedGeometry = MergeShapeLayerContent(context, stack, mergeMode);
            if (mergedGeometry is not null)
            {
                var result = context.ObjectFactory.CreateSpriteShape();
                result.Geometry = context.ObjectFactory.CreatePathGeometry(new CompositionPath(mergedGeometry));

                TranslateAndApplyShapeContext(
                    context,
                    result,
                    reverseDirection: false);

                return result;
            }
            else
            {
                return null;
            }
        }

        static CanvasGeometry? MergeShapeLayerContent(ShapeContext context, Stack<ShapeLayerContent> stack, MergePaths.MergeMode mergeMode)
        {
            var pathFillType = context.Fill is null ? ShapeFill.PathFillType.EvenOdd : context.Fill.FillType;
            var geometries = CreateCanvasGeometries(context, stack, pathFillType).ToArray();

            return geometries.Length switch
            {
                0 => null,
                1 => geometries[0],
                _ => CombineGeometries(context, geometries, mergeMode),
            };
        }

        // Combine all of the given geometries into a single geometry.
        static CanvasGeometry? CombineGeometries(
            TranslationContext context,
            CanvasGeometry[] geometries,
            MergePaths.MergeMode mergeMode)
        {
            switch (geometries.Length)
            {
                case 0:
                    return null;
                case 1:
                    return geometries[0];
            }

            // If MergeMode.Merge and they're all paths with the same FilledRegionDetermination,
            // combine into a single path.
            if (mergeMode == MergePaths.MergeMode.Merge &&
                geometries.All(g => g.Type == CanvasGeometry.GeometryType.Path) &&
                geometries.Select(g => ((CanvasGeometry.Path)g).FilledRegionDetermination).Distinct().Count() == 1)
            {
                return Paths.MergePaths(geometries.Cast<CanvasGeometry.Path>().ToArray());
            }
            else
            {
                if (geometries.Length > 50)
                {
                    // There will be stack overflows if the CanvasGeometry.Combine is too large.
                    // Usually not a problem, but handle degenerate cases.
                    context.Issues.MergingALargeNumberOfShapesIsNotSupported();
                    geometries = geometries.Take(50).ToArray();
                }

                var combineMode = ConvertTo.GeometryCombine(mergeMode);

#if PreCombineGeometries
            return CanvasGeometryCombiner.CombineGeometries(geometries, combineMode);
#else
                var accumulator = geometries[0];
                if (combineMode == CanvasGeometryCombine.Exclude)
                {
                    // TODO: investiagte how it works for 3+ layers with Exclude mode
                    for (var i = 1; i < geometries.Length; i++)
                    {
                        accumulator = geometries[i].CombineWith(accumulator, Sn.Matrix3x2.Identity, combineMode);
                    }
                }
                else
                {
                    for (var i = 1; i < geometries.Length; i++)
                    {
                        accumulator = accumulator.CombineWith(geometries[i], Sn.Matrix3x2.Identity, combineMode);
                    }
                }

                return accumulator;
#endif
            }
        }

        static IEnumerable<CanvasGeometry> CreateCanvasGeometries(
            ShapeContext context,
            Stack<ShapeLayerContent> stack,
            ShapeFill.PathFillType pathFillType)
        {
            while (stack.Count > 0)
            {
                // Ignore context on the stack - we only want geometries.
                var shapeContent = stack.Pop();
                switch (shapeContent.ContentType)
                {
                    case ShapeContentType.Group:
                        {
                            // Convert all the shapes in the group to a list of geometries
                            var group = (ShapeGroup)shapeContent;
                            var groupedGeometries = CreateCanvasGeometries(context.Clone(), new Stack<ShapeLayerContent>(group.Contents.ToArray()), pathFillType).ToArray();
                            foreach (var geometry in groupedGeometries)
                            {
                                yield return geometry;
                            }
                        }

                        break;
                    case ShapeContentType.MergePaths:
                        var mergedShapeLayerContent = MergeShapeLayerContent(context, stack, ((MergePaths)shapeContent).Mode);
                        if (mergedShapeLayerContent is not null)
                        {
                            yield return mergedShapeLayerContent;
                        }

                        break;
                    case ShapeContentType.Repeater:
                        context.Issues.RepeaterIsNotSupported();
                        break;
                    case ShapeContentType.Transform:
                        // TODO - do we need to clear out the transform when we've finished with this call to CreateCanvasGeometries?? Maybe the caller should clone the context.
                        context.SetTransform((Transform)shapeContent);
                        break;

                    case ShapeContentType.SolidColorStroke:
                    case ShapeContentType.LinearGradientStroke:
                    case ShapeContentType.RadialGradientStroke:
                    case ShapeContentType.SolidColorFill:
                    case ShapeContentType.RadialGradientFill:
                    case ShapeContentType.LinearGradientFill:
                    case ShapeContentType.TrimPath:
                    case ShapeContentType.RoundCorners:
                        // Ignore commands that set the context - we only want geometries.
                        break;

                    case ShapeContentType.Path:
                        yield return Paths.CreateWin2dPathGeometryFromShape(context, (Path)shapeContent, pathFillType, optimizeLines: true);
                        break;
                    case ShapeContentType.Ellipse:
                        yield return Ellipses.CreateWin2dEllipseGeometry(context, (Ellipse)shapeContent);
                        break;
                    case ShapeContentType.Rectangle:
                        yield return Rectangles.CreateWin2dRectangleGeometry(context, (Rectangle)shapeContent);
                        break;
                    case ShapeContentType.Polystar:
                        context.Issues.PolystarIsNotSupported();
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        static void TranslateAndApplyTrimPath(
            ShapeContext context,
            CompositionGeometry geometry,
            bool reverseDirection,
            double trimOffsetDegrees)
        {
            var trimPath = context.TrimPath;

            if (trimPath is null)
            {
                return;
            }

            if (reverseDirection)
            {
                trimPath = trimPath.CloneWithReversedDirection();
            }

            var startTrim = Optimizer.TrimAnimatable(context, trimPath.Start);
            var endTrim = Optimizer.TrimAnimatable(context, trimPath.End);
            var trimPathOffset = Optimizer.TrimAnimatable(context, trimPath.Offset);

            if (!startTrim.IsAnimated && !endTrim.IsAnimated)
            {
                // Handle some well-known static cases.
                if (startTrim.InitialValue.Value == 0 && endTrim.InitialValue.Value == 1)
                {
                    // The trim does nothing.
                    return;
                }
                else if (startTrim.InitialValue == endTrim.InitialValue)
                {
                    // TODO - the trim trims away all of the path.
                }
            }

            var order = GetAnimatableOrder(in startTrim, in endTrim);

            switch (order)
            {
                case AnimatableOrder.Before:
                case AnimatableOrder.Equal:
                    break;
                case AnimatableOrder.After:
                    {
                        // Swap is necessary to match the WinComp semantics.
                        var temp = startTrim;
                        startTrim = endTrim;
                        endTrim = temp;
                    }

                    break;
                case AnimatableOrder.BeforeAndAfter:
                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (order == AnimatableOrder.BeforeAndAfter)
            {
                // Add properties that will be animated. The TrimStart and TrimEnd properties
                // will be set by these values through an expression.
                Animate.TrimStartOrTrimEndPropertySetValue(context, startTrim, geometry, "TStart");
                var trimStartExpression = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.MinTStartTEnd);
                trimStartExpression.SetReferenceParameter("my", geometry);
                Animate.WithExpression(geometry, trimStartExpression, nameof(geometry.TrimStart));

                Animate.TrimStartOrTrimEndPropertySetValue(context, endTrim, geometry, "TEnd");
                var trimEndExpression = context.ObjectFactory.CreateExpressionAnimation(ExpressionFactory.MaxTStartTEnd);
                trimEndExpression.SetReferenceParameter("my", geometry);
                Animate.WithExpression(geometry, trimEndExpression, nameof(geometry.TrimEnd));
            }
            else
            {
                // Directly animate the TrimStart and TrimEnd properties.
                if (startTrim.IsAnimated)
                {
                    Animate.TrimStartOrTrimEnd(context, startTrim, geometry, nameof(geometry.TrimStart), "TrimStart", null);
                }
                else
                {
                    geometry.TrimStart = ConvertTo.Float(startTrim.InitialValue);
                }

                if (endTrim.IsAnimated)
                {
                    Animate.TrimStartOrTrimEnd(context, endTrim, geometry, nameof(geometry.TrimEnd), "TrimEnd", null);
                }
                else
                {
                    geometry.TrimEnd = ConvertTo.Float(endTrim.InitialValue);
                }
            }

            if (trimOffsetDegrees != 0 && !trimPathOffset.IsAnimated)
            {
                // Rectangle shapes are treated specially here to account for Lottie rectangle 0,0 being
                // top right and WinComp rectangle 0,0 being top left. As long as the TrimOffset isn't
                // being animated we can simply add an offset to the trim path.
                geometry.TrimOffset = (float)((trimPathOffset.InitialValue.Degrees + trimOffsetDegrees) / 360);
            }
            else
            {
                if (trimOffsetDegrees != 0)
                {
                    // TODO - can be handled with another property.
                    context.Issues.AnimatedTrimOffsetWithStaticTrimOffsetIsNotSupported();
                }

                if (trimPathOffset.IsAnimated)
                {
                    Animate.ScaledRotation(context, trimPathOffset, 1 / 360.0, geometry, nameof(geometry.TrimOffset), "TrimOffset", null);
                }
                else
                {
                    geometry.TrimOffset = ConvertTo.Float(trimPathOffset.InitialValue.Degrees / 360);
                }
            }
        }

        static AnimatableOrder GetAnimatableOrder(in TrimmedAnimatable<Trim> a, in TrimmedAnimatable<Trim> b)
        {
            var initialA = a.InitialValue.Value;
            var initialB = b.InitialValue.Value;

            var initialOrder = GetValueOrder(initialA, initialB);
            if (!a.IsAnimated && !b.IsAnimated)
            {
                return initialOrder;
            }

            // TODO - recognize more cases. For now just handle a is always before b
            var aMin = initialA;
            var aMax = initialA;
            if (a.IsAnimated)
            {
                aMin = Math.Min(a.KeyFrames.Min(kf => kf.Value.Value), initialA);
                aMax = Math.Max(a.KeyFrames.Max(kf => kf.Value.Value), initialA);
            }

            var bMin = initialB;
            var bMax = initialB;
            if (b.IsAnimated)
            {
                bMin = Math.Min(b.KeyFrames.Min(kf => kf.Value.Value), initialB);
                bMax = Math.Max(b.KeyFrames.Max(kf => kf.Value.Value), initialB);
            }

            switch (initialOrder)
            {
                case AnimatableOrder.Before:
                    return aMax <= bMin ? initialOrder : AnimatableOrder.BeforeAndAfter;
                case AnimatableOrder.After:
                    return aMin >= bMax ? initialOrder : AnimatableOrder.BeforeAndAfter;
                case AnimatableOrder.Equal:
                    {
                        if (aMin == aMax && bMin == bMax && aMin == bMax)
                        {
                            return AnimatableOrder.Equal;
                        }
                        else if (aMin < bMax)
                        {
                            // Might be before, unless they cross over.
                            return bMin < initialA || aMax > initialA ? AnimatableOrder.BeforeAndAfter : AnimatableOrder.Before;
                        }
                        else
                        {
                            // Might be after, unless they cross over.
                            return bMin > aMax ? AnimatableOrder.BeforeAndAfter : AnimatableOrder.After;
                        }
                    }

                case AnimatableOrder.BeforeAndAfter:
                default:
                    throw new InvalidOperationException();
            }
        }

        enum AnimatableOrder
        {
            Before,
            After,
            Equal,
            BeforeAndAfter,
        }

        static AnimatableOrder GetValueOrder(double a, double b)
        {
            if (a == b)
            {
                return AnimatableOrder.Equal;
            }
            else if (a < b)
            {
                return AnimatableOrder.Before;
            }
            else
            {
                return AnimatableOrder.After;
            }
        }

        // Discover patterns that we don't yet support and report any issues.
        static void CheckForUnsupportedShapeGroup(TranslationContext context, IReadOnlyList<ShapeLayerContent> contents)
        {
            // Count the number of geometries. More than 1 geometry is currently not properly supported
            // unless they're all paths.
            var pathCount = 0;
            var geometryCount = 0;

            for (var i = 0; i < contents.Count; i++)
            {
                switch (contents[i].ContentType)
                {
                    case ShapeContentType.Ellipse:
                    case ShapeContentType.Polystar:
                    case ShapeContentType.Rectangle:
                        geometryCount++;
                        break;
                    case ShapeContentType.Path:
                        pathCount++;
                        geometryCount++;
                        break;
                    default:
                        break;
                }
            }

            if (geometryCount > 1 && pathCount != geometryCount)
            {
                context.Issues.CombiningMultipleShapesIsNotSupported();
            }
        }

        static void CheckForRoundCornersOnPath(ShapeContext context)
        {
            if (!Optimizer.TrimAnimatable(context, context.RoundCorners.Radius).IsAlways(0))
            {
                context.Issues.PathWithRoundCornersIsNotFullySupported();
            }
        }

        sealed class ShapeLayerTranslator : LayerTranslator
        {
            readonly ShapeLayerContext _context;

            internal ShapeLayerTranslator(ShapeLayerContext context)
            {
                _context = context;
            }

            // Indicates if we can translate shape layer as a single composition shape (GetShapeRoot).
            // Otherwise we should use extra parent Visual (GetVisualRoot).
            //
            // Note: We can apply Masks and effects only to Visual node.
            // Also we can reduce number of expression animations if we apply opacity directly to visual node
            // instead of pushing it down to color fill that will produce color expression animation.
            internal override bool IsShape =>
                !_context.Layer.Masks.Any() &&
                _context.Effects.DropShadowEffect is null &&
                _context.Effects.GaussianBlurEffect is null;

            internal override CompositionShape? GetShapeRoot(TranslationContext context)
            {
                bool layerHasMasks = false;
#if !NoClipping
                layerHasMasks = _context.Layer.Masks.Any();
#endif
                if (layerHasMasks)
                {
                    throw new InvalidOperationException();
                }

                if (!Transforms.TryCreateContainerShapeTransformChain(_context, out var rootNode, out var contentsNode))
                {
                    // The layer is never visible.
                    return null;
                }

                var shapeContext = new ShapeContext(_context);

                // Update the opacity from the transform. This is necessary to push the opacity
                // to the leaves (because CompositionShape does not support opacity).
                // Note: this is no longer used because we will not call GetShapeRoot if layer transform has
                // animated opacity, instead we will call GetVisualRoot. But let's keep it here
                // just in case we will change the logic of IsShape flag.
                shapeContext.UpdateOpacityFromTransform(_context, _context.Layer.Transform);
                contentsNode.Shapes.Add(TranslateShapeLayerContents(shapeContext, _context.Layer.Contents));

                return rootNode;
            }

            internal override Visual? GetVisualRoot(CompositionContext context)
            {
                bool layerHasMasks = false;
#if !NoClipping
                layerHasMasks = _context.Layer.Masks.Any();
#endif

                if (!Transforms.TryCreateShapeVisualTransformChain(_context, out var rootNode, out var contentsNode))
                {
                    // The layer is never visible.
                    return null;
                }

                var shapeContext = new ShapeContext(_context);

                contentsNode.Shapes.Add(TranslateShapeLayerContents(shapeContext, _context.Layer.Contents));

                Visual result = layerHasMasks ? Masks.TranslateAndApplyMasksForLayer(_context, rootNode) : rootNode;

                var dropShadowEffect = _context.Effects.DropShadowEffect;

                if (dropShadowEffect is not null)
                {
                    result = Effects.ApplyDropShadow(_context, result, dropShadowEffect);
                }

                var gaussianBlurEffect = _context.Effects.GaussianBlurEffect;

                if (gaussianBlurEffect is not null)
                {
                    result = Effects.ApplyGaussianBlur(_context, result, gaussianBlurEffect);
                }

                return result;
            }
        }
    }
}