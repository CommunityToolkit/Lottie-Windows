// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    /// <summary>
    /// Methods that optimize <see cref="RenderingContext"/>s by precalculating
    /// values, combining contexts, replacing contexts with equivalent alternative
    /// contexts, and removing redundant context.
    /// </summary>
    static class ContextOptimizers
    {
        internal static RenderingContext Optimize(RenderingContext input)
        {
            AssertUniformTimebase(input);

            var result = input;

            // Get the metadata.
            var metadata = MetadataRenderingContext.Compose(result.OfType<MetadataRenderingContext>());

            var beforeOptimization = result;

            // Eliminate the metadata.
            result = result.Without<MetadataRenderingContext>();

            // Keep running the optimizer as long as it makes progress.
            while (TryOptimizeOnce(ref result))
            {
            }

            return metadata + result;
        }

        static bool TryOptimizeOnce(ref RenderingContext context)
        {
            AssertUniformTimebase(context);

            var success = false;

            // Replace any anchors with equivalent centerpoints and positions.
            success |= TryReplaceAnchors(ref context);

            // The last blending mode wins.
            success |= TryOptimizeBlendModes(ref context);

            success |= TryOptimizeOpacity(ref context);
            success |= TryOptimizePosition(ref context);
            success |= TryOptimizeRotation(ref context);
            success |= TryOptimizeScale(ref context);
            success |= TryOptimizeSize(ref context);
            success |= TryOptimizeGradientFillsWithPosition(ref context);
            success |= TryOptimizeGradientStrokesWithPosition(ref context);
            success |= TryOptimizeVisibility(ref context);

            return success;
        }

        /// <summary>
        /// Attempts to move the static scale to the bottom of the context. This
        /// makes it easy to remove the scale from the context and apply it to
        /// the content instead.
        /// </summary>
        /// <returns><c>true</c> if the context was modified.</returns>
        internal static bool TryMoveStaticScaleToBottom(ref RenderingContext context)
        {
            var success = false;

            // Move the scales as low as they can be.
            success |= context.TryGroupDown<ScaleRenderingContext>(out context);

            // See if we can move the bottom scale lower.
            if (context.SubContextCount > 1)
            {
                var subContexts = context.ToArray();
                ref var secondLast = ref subContexts[subContexts.Length - 2];
                ref var last = ref subContexts[subContexts.Length - 1];

                if (secondLast is ScaleRenderingContext.Static staticscale)
                {
                    // For now we only handle one case.
                    if (last is FillRenderingContext fill)
                    {
                        // This is the case we can handle. The bottom context is a fill
                        // and the context above it is a static scale. Swap the scale and
                        // the fill, and scale the fill.
                        secondLast = new FillRenderingContext(fill.Brush.WithScale(staticscale.ScalePercent / 100.0));
                        last = staticscale;
                        context = RenderingContext.Compose(subContexts);
                        success = true;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Replaces any <see cref="AnchorRenderingContext"/>s with <see cref="PositionRenderingContext"/>s
        /// and <see cref="CenterPointRenderingContext"/>s.
        /// </summary>
        /// <returns><c>true</c> if the context was modified.</returns>
        internal static bool TryReplaceAnchors(ref RenderingContext context)
        {
            var success = false;
            context = RenderingContext.Compose(Replace(context));
            return success;

            IEnumerable<RenderingContext> Replace(RenderingContext context)
            {
                AnchorRenderingContext currentAnchor = AnchorRenderingContext.Static.Zero;

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case AnchorRenderingContext anchor:
                            // Store the anchor.
                            currentAnchor = anchor;

                            // The anchor moves the position to the inverse of the anchor
                            // (i.e. the anchor is the point that is positioned by the position).
                            success = true;
                            yield return currentAnchor.ToPositionRenderingContext(value => -value);
                            break;

                        case RotationRenderingContext:
                        case ScaleRenderingContext:
                            // Rotation/scale is done around the anchor or centerpoint (or 0,0 if neither is specified).
                            // Instead of an anchor, output the position offset that is equivalent to the anchor,
                            // then the rotation/scale, then the inverse of the position offset that is equivalent to
                            // the anchor.
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated animatedAnchor:
                                    // Animated anchor. Use an animated position.
                                    yield return new PositionRenderingContext.Animated(animatedAnchor.Anchor);
                                    yield return item;
                                    yield return new PositionRenderingContext.Animated(animatedAnchor.Anchor.Inverted());
                                    break;

                                case AnchorRenderingContext.Static staticAnchor:
                                    if (staticAnchor.Anchor == Vector2.Zero)
                                    {
                                        yield return item;
                                    }
                                    else
                                    {
                                        yield return new PositionRenderingContext.Static(staticAnchor.Anchor);
                                        yield return item;
                                        yield return new PositionRenderingContext.Static(-staticAnchor.Anchor);
                                    }

                                    break;

                                default: throw Unreachable;
                            }

                            break;

                        default:
                            yield return item;
                            break;
                    }
                }
            }
        }

        internal static bool TryOptimizeBlendModes(ref RenderingContext context)
        {
            AssertUniformTimebase(context);

            var initialCount = context.SubContextCount;
            context = RenderingContext.Compose(Optimize(context));
            return initialCount != context.SubContextCount;

            static IEnumerable<RenderingContext> Optimize(RenderingContext items)
            {
                // Remove all but the last BlendMode and put it at the end of the list.
                BlendModeRenderingContext? lastBlendMode = null;

                foreach (var item in items)
                {
                    if (item is BlendModeRenderingContext blendMode)
                    {
                        lastBlendMode = blendMode;
                    }
                    else
                    {
                        yield return item;
                    }
                }

                if (lastBlendMode != null && lastBlendMode.BlendMode != BlendMode.Normal)
                {
                    yield return lastBlendMode;
                }
            }
        }

        /// <summary>
        /// Moves positions above gradient fills by offsetting
        /// the fills by the amount of the position.
        /// </summary>
        /// <returns>The context with positions moved above the fills.</returns>
        public static bool TryOptimizeGradientFillsWithPosition(ref RenderingContext context)
        {
            AssertUniformTimebase(context);

            var success = false;
            context = RenderingContext.Compose(OptimizeFills(context.GroupUp<PositionRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> OptimizeFills(IEnumerable<RenderingContext> items)
            {
                var lastWasFill = false;
                FillRenderingContext? currentFill = null;

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case FillRenderingContext fill:
                            currentFill = fill;
                            lastWasFill = true;
                            break;

                        case PositionRenderingContext.Static position:
                            yield return position;
                            if (currentFill != null)
                            {
                                success = true;
                                yield return currentFill.WithOffset(-position.Position);
                            }

                            currentFill = null;
                            lastWasFill = false;
                            break;

                        default:
                            if (currentFill != null)
                            {
                                yield return currentFill;
                                currentFill = null;
                            }

                            yield return item;
                            lastWasFill = false;
                            break;
                    }
                }

                if (currentFill != null)
                {
                    if (!lastWasFill)
                    {
                        success = true;
                    }

                    yield return currentFill;
                }
            }
        }

        /// <summary>
        /// Moves positions above gradient strokes by offsetting
        /// the strokes by the amount of the position.
        /// </summary>
        /// <returns>The context with positions moved above the strokes.</returns>
        public static bool TryOptimizeGradientStrokesWithPosition(ref RenderingContext context)
        {
            AssertUniformTimebase(context);

            var success = false;
            context = RenderingContext.Compose(OptimizeStrokes(context.GroupUp<PositionRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> OptimizeStrokes(IEnumerable<RenderingContext> items)
            {
                var lastWasStroke = false;
                StrokeRenderingContext? currentStroke = null;

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case StrokeRenderingContext stroke:
                            currentStroke = stroke;
                            lastWasStroke = true;
                            break;

                        case PositionRenderingContext.Static position:
                            yield return position;
                            if (currentStroke != null)
                            {
                                success = true;
                                yield return currentStroke.WithOffset(-position.Position);
                            }

                            currentStroke = null;
                            lastWasStroke = false;
                            break;

                        default:
                            if (currentStroke != null)
                            {
                                success = true;
                                yield return currentStroke;
                                currentStroke = null;
                            }

                            yield return item;
                            lastWasStroke = false;
                            break;
                    }
                }

                if (currentStroke != null)
                {
                    if (!lastWasStroke)
                    {
                        success = true;
                    }

                    yield return currentStroke;
                }
            }
        }

        public static bool TryOptimizeOpacity(ref RenderingContext context)
        {
            AssertUniformTimebase(context);
            var success = false;

            context = RenderingContext.Compose(Optimize(context.GroupUp<OpacityRenderingContext>()));

            return success;

            // Assumes the opacities have already been grouped.
            IEnumerable<RenderingContext> Optimize(RenderingContext items)
            {
                var lastWasOpacity = false;
                var opacitiesAccumulator = new List<OpacityRenderingContext>();

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case OpacityRenderingContext opacity:
                            opacitiesAccumulator.Add(opacity);
                            lastWasOpacity = true;
                            break;

                        default:
                            switch (opacitiesAccumulator.Count)
                            {
                                case 0:
                                    break;

                                case 1:
                                    yield return opacitiesAccumulator[0];
                                    if (!lastWasOpacity)
                                    {
                                        success = true;
                                    }

                                    break;

                                default:
                                    success = true;
                                    foreach (var opacity in OpacityRenderingContext.Combine(opacitiesAccumulator))
                                    {
                                        yield return opacity;
                                    }

                                    break;
                            }

                            yield return item;
                            opacitiesAccumulator.Clear();
                            lastWasOpacity = false;
                            break;
                    }
                }

                foreach (var opacity in OpacityRenderingContext.Combine(opacitiesAccumulator))
                {
                    if (!lastWasOpacity)
                    {
                        success = true;
                    }

                    yield return opacity;
                }
            }
        }

        public static bool TryOptimizePosition(ref RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            var success = false;
            context = RenderingContext.Compose(Optimize(context.GroupUp<PositionRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var lastWasStaticPosition = false;
                var currentOffset = Vector2.Zero;

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case PositionRenderingContext.Static staticPosition:
                            currentOffset += staticPosition.Position;
                            lastWasStaticPosition = true;
                            break;

                        case PositionRenderingContext.Animated animatedPosition:
                            if (currentOffset != Vector2.Zero)
                            {
                                success = true;
                            }

                            yield return animatedPosition.WithOffset(currentOffset);
                            currentOffset = Vector2.Zero;
                            lastWasStaticPosition = false;
                            break;

                        default:
                            if (currentOffset != Vector2.Zero)
                            {
                                if (!lastWasStaticPosition)
                                {
                                    success = true;
                                }

                                yield return new PositionRenderingContext.Static(currentOffset);
                                currentOffset = Vector2.Zero;
                            }

                            yield return item;
                            lastWasStaticPosition = false;
                            break;
                    }
                }

                if (currentOffset != Vector2.Zero)
                {
                    if (!lastWasStaticPosition)
                    {
                        success = true;
                    }

                    yield return new PositionRenderingContext.Static(currentOffset);
                }
            }
        }

        public static bool TryOptimizeRotation(ref RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            var success = false;
            context = RenderingContext.Compose(Optimize(context.GroupUp<RotationRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var currentRotation = Rotation.None;

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case RotationRenderingContext.Static staticRotation:
                            currentRotation += staticRotation.Rotation;
                            break;

                        case RotationRenderingContext.Animated animatedRotation:
                            if (currentRotation == Rotation.None)
                            {
                                yield return item;
                            }
                            else
                            {
                                // Multiply the current rotation into this animated rotation.
                                success = true;
                                yield return new RotationRenderingContext.Animated(animatedRotation.Rotation.Select(r => r + currentRotation));
                                currentRotation = Rotation.None;
                            }

                            break;

                        default:
                            if (currentRotation != Rotation.None)
                            {
                                success = true;
                                yield return new RotationRenderingContext.Static(currentRotation);
                                currentRotation = Rotation.None;
                            }

                            yield return item;
                            break;
                    }
                }

                if (currentRotation != Rotation.None)
                {
                    success = true;
                    yield return new RotationRenderingContext.Static(currentRotation);
                }
            }
        }

        /// <summary>
        /// Combines multiple scales.
        /// </summary>
        /// <returns>Optimized <see cref="RenderingContext"/>.</returns>
        public static bool TryOptimizeScale(ref RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            var success = false;
            context = RenderingContext.Compose(Optimize(context.GroupUp<ScaleRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var lastWasStaticScale = false;
                var currentScale = Vector2.One;

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case ScaleRenderingContext.Static staticScale:
                            currentScale *= staticScale.ScalePercent / 100;
                            lastWasStaticScale = true;
                            break;

                        case ScaleRenderingContext.Animated animatedScale:
                            if (currentScale != Vector2.One && !lastWasStaticScale)
                            {
                                success = true;
                            }

                            switch (animatedScale.ScalePercent.Type)
                            {
                                case AnimatableVector2Type.Vector2:
                                    yield return new ScaleRenderingContext.Animated(((AnimatableVector2)animatedScale.ScalePercent).Select(s => s * currentScale));
                                    break;

                                case AnimatableVector2Type.XY:
                                    yield return new ScaleRenderingContext.Animated(((AnimatableXY)animatedScale.ScalePercent).Select(x => x * currentScale.X, y => y * currentScale.Y));
                                    break;

                                default: throw Unreachable;
                            }

                            lastWasStaticScale = false;
                            break;

                        default:
                            if (currentScale != Vector2.One)
                            {
                                if (!lastWasStaticScale)
                                {
                                    success = true;
                                }

                                yield return new ScaleRenderingContext.Static(currentScale * 100);
                                currentScale = Vector2.One;
                            }

                            yield return item;
                            lastWasStaticScale = false;
                            break;
                    }
                }

                if (currentScale != Vector2.One)
                {
                    if (!lastWasStaticScale)
                    {
                        success = true;
                    }

                    yield return new ScaleRenderingContext.Static(currentScale * 100);
                }
            }
        }

        public static bool TryOptimizeSize(ref RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            var success = false;
            context = RenderingContext.Compose(Optimize(context.GroupUp<SizeRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> Optimize(RenderingContext items)
            {
                // Keep track of the bounding box.
                var boundingBoxAdjusted = false;
                var topLeft = Vector2.Zero;
                var bottomRight = new Vector2(double.PositiveInfinity, double.PositiveInfinity);

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case SizeRenderingContext size:
                            var newBottomRight = new Vector2(Math.Min(bottomRight.X, size.Size.X), Math.Min(bottomRight.Y, size.Size.Y));
                            if (newBottomRight != bottomRight)
                            {
                                if (boundingBoxAdjusted)
                                {
                                    success = true;
                                }

                                yield return size;
                                bottomRight = newBottomRight;
                            }

                            continue;

                        case ScaleRenderingContext.Static staticScale:
                            // Update the bounding box.
                            topLeft *= staticScale.ScalePercent / 100;
                            bottomRight *= staticScale.ScalePercent / 100;
                            boundingBoxAdjusted = true;
                            break;

                        case PositionRenderingContext.Static staticPosition:
                            // Update the bounding box.
                            topLeft += staticPosition.Position;
                            bottomRight += staticPosition.Position;
                            boundingBoxAdjusted = true;
                            break;

                        case RotationRenderingContext _:
                        case ScaleRenderingContext.Animated _:
                        case PositionRenderingContext.Animated _:
                            // Reset the bounding box.
                            topLeft = Vector2.Zero;
                            bottomRight = new Vector2(double.PositiveInfinity, double.PositiveInfinity);
                            boundingBoxAdjusted = true;
                            break;
                    }

                    yield return item;
                }
            }
        }

        public static bool TryOptimizeVisibility(ref RenderingContext context)
        {
            AssertUniformTimebase(context);

            var success = false;
            context = RenderingContext.Compose(Optimize(context.GroupUp<VisibilityRenderingContext>()));
            return success;

            IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var lastWasVisibility = false;
                var visibilities = new List<VisibilityRenderingContext>();
                foreach (var subContext in context)
                {
                    switch (subContext)
                    {
                        case VisibilityRenderingContext visibilityContext:
                            visibilities.Add(visibilityContext);
                            lastWasVisibility = true;
                            break;

                        default:
                            switch (visibilities.Count)
                            {
                                case 0:
                                    break;

                                case 1:
                                    yield return visibilities[0];

                                    if (!lastWasVisibility)
                                    {
                                        success = true;
                                    }

                                    break;

                                default:
                                    yield return VisibilityRenderingContext.Combine(visibilities);
                                    success = true;
                                    break;
                            }

                            yield return subContext;
                            lastWasVisibility = false;
                            visibilities.Clear();
                            break;
                    }
                }
            }
        }

        public static RenderingContext UnifyTimebase(RenderingContext context)
        {
            return RenderingContext.Compose(Unify(context));

            static IEnumerable<RenderingContext> Unify(RenderingContext context)
            {
                var timeOffset = 0.0;

                foreach (var subContext in context)
                {
                    switch (subContext)
                    {
                        case TimeOffsetRenderingContext timeOffsetContext:
                            timeOffset += timeOffsetContext.TimeOffset;
                            break;
                        default:
                            yield return subContext.WithTimeOffset(timeOffset);
                            break;
                    }
                }
            }
        }

        [Conditional("DEBUG")]
        static void AssertNoAnchors(RenderingContext context)
        {
            if (context.Contains<AnchorRenderingContext>())
            {
                throw new InvalidOperationException("Unexpected anchor");
            }
        }

        [Conditional("DEBUG")]
        internal static void AssertUniformTimebase(RenderingContext context)
        {
            if (context.Contains<TimeOffsetRenderingContext>())
            {
                throw new InvalidOperationException("Non-uniform timebase");
            }
        }
    }
}