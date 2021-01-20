// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Brushes;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.Layers;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    static class ContextOptimizers
    {
        internal static RenderingContext Optimize(RenderingContext input)
        {
            AssertUniformTimebase(input);

            var result = input;

            // Eliminate the metadata.
            result = ElideMetadata(result);

            // Replace any anchors with equivalent centerpoints and positions.
            result = ReplaceAnchors(result);

            result = OptimizeBlendModes(result);
            result = OptimizeOpacity(result);
            result = OptimizePosition(result);
            result = OptimizeRotation(result);
            result = OptimizeScale(result);
            result = OptimizeSize(result);
            result = OptimizeVisibility(result);

            return result;
        }

        internal static RenderingContext ElideMetadata(RenderingContext input)
            => input.Filter((MetadataRenderingContext c) => false);

        /// <summary>
        /// Replaces any <see cref="AnchorRenderingContext"/>s with <see cref="PositionRenderingContext"/>s
        /// and <see cref="CenterPointRenderingContext"/>s.
        /// </summary>
        /// <returns>A new <see cref="RenderingContext"/> that contains no
        /// <see cref="AnchorRenderingContext"/>s.</returns>
        internal static RenderingContext ReplaceAnchors(RenderingContext context)
        {
            return RenderingContext.Compose(Replace(context));

            static IEnumerable<RenderingContext> Replace(RenderingContext context)
            {
                AnchorRenderingContext currentAnchor = new AnchorRenderingContext.Static(Vector2.Zero);

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case AnchorRenderingContext anchor:
                            // Store the anchor. It scale, and rotation.
                            currentAnchor = anchor;

                            // The anchor moves the position to the inverse of the anchor
                            // (i.e. the anchor is the point that is positioned by the position).
                            yield return currentAnchor.ToPositionRenderingContext(value => Vector2.Zero - value);
                            break;

                        case RotationRenderingContext.Animated rotation:
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated animatedAnchor:
                                    // Animated anchor and animated rotation. Use an animated centerpoint.
                                    yield return new CenterPointRenderingContext.Animated(animatedAnchor.Anchor);
                                    yield return item;

                                    // Reset the centerpoint.
                                    yield return new CenterPointRenderingContext.Static(Vector2.Zero);
                                    break;
                                case AnchorRenderingContext.Static staticAnchor:
                                    // Static anchor and animated rotation.
                                    yield return new PositionRenderingContext.Animated(new AnimatableVector2(rotation.Rotation.Select(r =>
                                        r.RotatePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor)).KeyFrames));
                                    yield return item;
                                    break;

                                default: throw Unreachable;
                            }

                            break;

                        case RotationRenderingContext.Static rotation:
                            // Static anchor and static rotation.
                            yield return currentAnchor.ToPositionRenderingContext(
                                value => rotation.Rotation.RotatePointAroundOrigin(point: Vector2.Zero, origin: value));
                            yield return item;
                            break;

                        case ScaleRenderingContext.Animated scale:
                            switch (currentAnchor)
                            {
                                case AnchorRenderingContext.Animated animatedAnchor:
                                    // Animated anchor and animated scale. Use an animated centerpoint.
                                    yield return new CenterPointRenderingContext.Animated(animatedAnchor.Anchor);
                                    yield return item;

                                    // Reset the centerpoint.
                                    yield return new CenterPointRenderingContext.Static(Vector2.Zero);
                                    break;
                                case AnchorRenderingContext.Static staticAnchor:
                                    // Static anchor and animated scale.
                                    switch (scale.ScalePercent)
                                    {
                                        case AnimatableVector2 scale2:
                                            yield return new PositionRenderingContext.Animated(new AnimatableVector2(scale2.Select(s =>
                                                ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor, scale: s / 100)).KeyFrames));
                                            break;

                                        case AnimatableXY scaleXY:
                                            // The scale has X and Y animated separately.
                                            yield return new PositionRenderingContext.Animated(scaleXY.Select(
                                                    sX => ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor, scale: new Vector2(sX / 100, 0)).X,
                                                    sY => ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: staticAnchor.Anchor, scale: new Vector2(0, sY / 100)).Y));
                                            break;

                                        default: throw Unreachable;
                                    }

                                    yield return item;
                                    break;

                                default: throw Unreachable;
                            }

                            break;

                        case ScaleRenderingContext.Static scale:
                            yield return currentAnchor.ToPositionRenderingContext(
                                value => ScaleRenderingContext.ScalePointAroundOrigin(point: Vector2.Zero, origin: value, scale: scale.ScalePercent / 100));
                            yield return item;
                            break;

                        default:
                            yield return item;
                            break;
                    }
                }
            }
        }

        internal static RenderingContext OptimizeBlendModes(RenderingContext context)
        {
            AssertUniformTimebase(context);

            // Remove all but the last BlendMode.
            return context.SubContextCount > 0
                    ? RenderingContext.Compose(Optimize(context))
                    : context;

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

        public static RenderingContext OptimizeOpacity(RenderingContext context)
        {
            AssertUniformTimebase(context);

            return RenderingContext.Compose(Optimize(context.GroupUp<OpacityRenderingContext>()));

            // Assumes the opacities have already been grouped.
            static IEnumerable<RenderingContext> Optimize(RenderingContext items)
            {
                var previousOpacityIsOpaque = true;
                var opacitiesAccumulator = new List<OpacityRenderingContext>();

                foreach (var item in items)
                {
                    switch (item)
                    {
                        case OpacityRenderingContext opacity:
                            opacitiesAccumulator.Add((OpacityRenderingContext)opacity);
                            break;

                        default:
                            if (opacitiesAccumulator.Count > 0)
                            {
                                foreach (var opacity in OpacityRenderingContext.Combine(opacitiesAccumulator))
                                {
                                    var opacityIsOpaque = opacity is OpacityRenderingContext.Static staticOpacity && staticOpacity.Opacity == Opacity.Opaque;

                                    if (previousOpacityIsOpaque && opacityIsOpaque)
                                    {
                                        // No point outputing a static opaque opacity if the last one was
                                        // opaque.
                                    }
                                    else
                                    {
                                        yield return opacity;
                                        previousOpacityIsOpaque = opacityIsOpaque;
                                    }
                                }

                                opacitiesAccumulator.Clear();
                            }

                            yield return item;
                            break;
                    }
                }

                foreach (var opacity in OpacityRenderingContext.Combine(opacitiesAccumulator))
                {
                    yield return opacity;
                }
            }
        }

        public static RenderingContext OptimizePosition(RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            return RenderingContext.Compose(Optimize(context.GroupUp<PositionRenderingContext>()));

            static IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var currentOffset = Vector2.Zero;

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case PositionRenderingContext.Static staticPosition:
                            currentOffset += staticPosition.Position;
                            break;

                        case PositionRenderingContext.Animated animatedPosition:
                            yield return animatedPosition.WithOffset(currentOffset);
                            currentOffset = Vector2.Zero;
                            break;

                        default:
                            if (currentOffset != Vector2.Zero)
                            {
                                yield return new PositionRenderingContext.Static(currentOffset);
                                currentOffset = Vector2.Zero;
                            }

                            yield return item;
                            break;
                    }
                }

                if (currentOffset != Vector2.Zero)
                {
                    yield return new PositionRenderingContext.Static(currentOffset);
                }
            }
        }

        public static RenderingContext OptimizeRotation(RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            return RenderingContext.Compose(Optimize(context.GroupUp<RotationRenderingContext>()));

            static IEnumerable<RenderingContext> Optimize(RenderingContext context)
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
                            yield return new RotationRenderingContext.Animated(animatedRotation.Rotation.Select(r => r + currentRotation));
                            currentRotation = Rotation.None;
                            break;

                        default:
                            if (currentRotation != Rotation.None)
                            {
                                yield return new RotationRenderingContext.Static(currentRotation);
                                currentRotation = Rotation.None;
                            }

                            yield return item;
                            break;
                    }
                }

                if (currentRotation != Rotation.None)
                {
                    yield return new RotationRenderingContext.Static(currentRotation);
                }
            }
        }

        public static RenderingContext OptimizeScale(RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            return RenderingContext.Compose(Optimize(context.GroupUp<ScaleRenderingContext>()));

            static IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var currentScale = Vector2.One;

                foreach (var item in context)
                {
                    switch (item)
                    {
                        case ScaleRenderingContext.Static staticScale:
                            currentScale *= staticScale.ScalePercent / 100;
                            break;

                        case ScaleRenderingContext.Animated animatedScale:
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

                            break;

                        default:
                            if (currentScale != Vector2.One)
                            {
                                yield return new ScaleRenderingContext.Static(currentScale * 100);
                                currentScale = Vector2.One;
                            }

                            yield return item;
                            break;
                    }
                }

                if (currentScale != Vector2.One)
                {
                    yield return new ScaleRenderingContext.Static(currentScale * 100);
                }
            }
        }

        public static RenderingContext OptimizeSize(RenderingContext context)
        {
            AssertUniformTimebase(context);
            AssertNoAnchors(context);

            return RenderingContext.Compose(Optimize(context.GroupUp<SizeRenderingContext>()));

            static IEnumerable<RenderingContext> Optimize(RenderingContext items)
            {
                // Keep track of the bounding box.
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
                                yield return size;
                                bottomRight = newBottomRight;
                            }

                            continue;

                        case ScaleRenderingContext.Static staticScale:
                            // Update the bounding box.
                            topLeft *= staticScale.ScalePercent / 100;
                            bottomRight *= staticScale.ScalePercent / 100;
                            break;

                        case PositionRenderingContext.Static staticPosition:
                            // Update the bounding box.
                            topLeft += staticPosition.Position;
                            bottomRight += staticPosition.Position;
                            break;

                        case RotationRenderingContext _:
                        case ScaleRenderingContext.Animated _:
                        case PositionRenderingContext.Animated _:
                            // Reset the bounding box.
                            topLeft = Vector2.Zero;
                            bottomRight = new Vector2(double.PositiveInfinity, double.PositiveInfinity);
                            break;
                    }

                    yield return item;
                }
            }
        }

        public static RenderingContext OptimizeVisibility(RenderingContext context)
        {
            AssertUniformTimebase(context);

            return RenderingContext.Compose(Optimize(context.GroupUp<VisibilityRenderingContext>()));

            static IEnumerable<RenderingContext> Optimize(RenderingContext context)
            {
                var visibilities = new List<VisibilityRenderingContext>();
                foreach (var subContext in context)
                {
                    switch (subContext)
                    {
                        case VisibilityRenderingContext visibilityContext:
                            visibilities.Add(visibilityContext);
                            break;

                        default:
                            if (visibilities.Count > 0)
                            {
                                yield return VisibilityRenderingContext.Combine(visibilities);
                                visibilities.Clear();
                            }

                            yield return subContext;
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
        static void AssertUniformTimebase(RenderingContext context)
        {
            if (context.Contains<TimeOffsetRenderingContext>())
            {
                throw new InvalidOperationException("Non-uniform timebase");
            }
        }
    }
}