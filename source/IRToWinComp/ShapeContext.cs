// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// Describes the environment in which a Lottie shape should be interpreted.
    /// </summary>
    sealed class ShapeContext
    {
        // A RoundCorners with a radius of 0.
        static readonly RoundCorners s_defaultRoundCorners =
            new RoundCorners(new ShapeLayerContent.ShapeLayerContentArgs { }, new Animatable<double>(0));

        internal ShapeContext(ShapeLayerContext layer)
        {
            LayerContext = layer;
            ObjectFactory = layer.ObjectFactory;
        }

        public ShapeLayerContext LayerContext { get; }

        public CompositionObjectFactory ObjectFactory { get; }

        public TranslationContext Translation => LayerContext.CompositionContext.Translation;

        public TranslationIssues Issues => Translation.Issues;

        internal ShapeStroke? Stroke { get; private set; }

        internal ShapeFill? Fill { get; private set; }

        internal TrimPath? TrimPath { get; private set; }

        /// <summary>
        /// Never null. If there is no <see cref="RoundCorners"/> set, a default
        /// 0 <see cref="RoundCorners"/> will be returned.
        /// </summary>
        internal RoundCorners RoundCorners { get; private set; } = s_defaultRoundCorners;

        internal Transform? Transform { get; private set; }

        // Opacity is not part of the Lottie context for shapes. But because WinComp
        // doesn't support opacity on shapes, the opacity is inherited from
        // the Transform and passed through to the brushes here.
        internal CompositeOpacity Opacity { get; private set; } = CompositeOpacity.Opaque;

        internal void UpdateFromStack(Stack<ShapeLayerContent> stack)
        {
            while (stack.Count > 0)
            {
                var popped = stack.Peek();
                switch (popped.ContentType)
                {
                    case ShapeContentType.LinearGradientFill:
                    case ShapeContentType.RadialGradientFill:
                    case ShapeContentType.SolidColorFill:
                        Fill = ComposeFills(Fill, (ShapeFill)popped);
                        break;

                    case ShapeContentType.LinearGradientStroke:
                    case ShapeContentType.RadialGradientStroke:
                    case ShapeContentType.SolidColorStroke:
                        Stroke = ComposeStrokes(Stroke, (ShapeStroke)popped);
                        break;

                    case ShapeContentType.RoundCorners:
                        RoundCorners = ComposeRoundCorners(RoundCorners, (RoundCorners)popped);
                        break;

                    case ShapeContentType.TrimPath:
                        TrimPath = ComposeTrimPaths(TrimPath, (TrimPath)popped);
                        break;

                    default: return;
                }

                stack.Pop();
            }
        }

        internal void UpdateOpacityFromTransform(LayerContext context, Transform transform)
        {
            if (transform is null)
            {
                return;
            }

            Opacity = Opacity.ComposedWith(Optimizer.TrimAnimatable(context, transform.Opacity));
        }

        // Only used when translating geometries. Layers use an extra Shape or Visual to
        // apply the transform, but geometries need to take the transform into account when
        // they're created.
        internal void SetTransform(Transform transform)
        {
            Transform = transform;
        }

        internal ShapeContext Clone() =>
            new ShapeContext(LayerContext)
            {
                Fill = Fill,
                Stroke = Stroke,
                TrimPath = TrimPath,
                RoundCorners = RoundCorners,
                Opacity = Opacity,
                Transform = Transform,
            };

        ShapeFill? ComposeFills(ShapeFill? a, ShapeFill? b)
        {
            if (a is null)
            {
                return b;
            }
            else if (b is null)
            {
                return a;
            }

            if (a.FillKind != b.FillKind)
            {
                Translation.Issues.MultipleFillsIsNotSupported();
                return b;
            }

            switch (a.FillKind)
            {
                case ShapeFill.ShapeFillKind.SolidColor:
                    return ComposeSolidColorFills((SolidColorFill)a, (SolidColorFill)b);
            }

            Translation.Issues.MultipleFillsIsNotSupported();
            return b;
        }

        SolidColorFill ComposeSolidColorFills(SolidColorFill a, SolidColorFill b)
        {
            if (!b.Color.IsAnimated && !b.Opacity.IsAnimated)
            {
                if (b.Opacity.InitialValue == Animatables.Opacity.Opaque &&
                    b.Color.InitialValue.A == 1)
                {
                    // b overrides a.
                    return b;
                }
                else if (b.Opacity.InitialValue.IsTransparent || b.Color.InitialValue.A == 0)
                {
                    // b is transparent, so a wins.
                    return a;
                }
            }

            Translation.Issues.MultipleFillsIsNotSupported();
            return b;
        }

        ShapeStroke? ComposeStrokes(ShapeStroke? a, ShapeStroke? b)
        {
            if (a is null)
            {
                return b;
            }
            else if (b is null)
            {
                return a;
            }

            if (a.StrokeKind != b.StrokeKind)
            {
                Translation.Issues.MultipleStrokesIsNotSupported();
                return b;
            }

            return a.StrokeKind switch
            {
                ShapeStroke.ShapeStrokeKind.SolidColor => ComposeSolidColorStrokes((SolidColorStroke)a, (SolidColorStroke)b),
                ShapeStroke.ShapeStrokeKind.LinearGradient => ComposeLinearGradientStrokes((LinearGradientStroke)a, (LinearGradientStroke)b),
                ShapeStroke.ShapeStrokeKind.RadialGradient => ComposeRadialGradientStrokes((RadialGradientStroke)a, (RadialGradientStroke)b),
                _ => throw new InvalidOperationException(),
            };
        }

        LinearGradientStroke ComposeLinearGradientStrokes(LinearGradientStroke a, LinearGradientStroke b)
        {
            if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                a.Opacity.IsAlways(Animatables.Opacity.Opaque) && b.Opacity.IsAlways(Animatables.Opacity.Opaque))
            {
                if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                {
                    // a occludes b, so b can be ignored.
                    return a;
                }
            }

            Translation.Issues.MultipleStrokesIsNotSupported();
            return a;
        }

        RadialGradientStroke ComposeRadialGradientStrokes(RadialGradientStroke a, RadialGradientStroke b)
        {
            if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                a.Opacity.IsAlways(Animatables.Opacity.Opaque) && b.Opacity.IsAlways(Animatables.Opacity.Opaque))
            {
                if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                {
                    // a occludes b, so b can be ignored.
                    return a;
                }
            }

            Translation.Issues.MultipleStrokesIsNotSupported();
            return a;
        }

        SolidColorStroke ComposeSolidColorStrokes(SolidColorStroke a, SolidColorStroke b)
        {
            if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                !a.DashPattern.Any() && !b.DashPattern.Any() &&
                a.Opacity.IsAlways(Animatables.Opacity.Opaque) && b.Opacity.IsAlways(Animatables.Opacity.Opaque))
            {
                if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                {
                    // a occludes b, so b can be ignored.
                    return a;
                }
            }

            // The new stroke should be in addition to the existing stroke. And colors should blend.
            Translation.Issues.MultipleStrokesIsNotSupported();
            return b;
        }

        RoundCorners ComposeRoundCorners(RoundCorners a, RoundCorners b)
        {
            if (a is null)
            {
                return b;
            }
            else if (b is null)
            {
                return a;
            }

            if (!b.Radius.IsAnimated)
            {
                if (b.Radius.InitialValue >= 0)
                {
                    // If b has a non-0 value, it wins.
                    return b;
                }
                else
                {
                    // b is always 0. A wins.
                    return a;
                }
            }

            Translation.Issues.MultipleAnimatedRoundCornersIsNotSupported();
            return b;
        }

        TrimPath? ComposeTrimPaths(TrimPath? a, TrimPath? b)
        {
            if (a is null)
            {
                return b;
            }
            else if (b is null)
            {
                return a;
            }

            if (!a.Start.IsAnimated && !a.Start.IsAnimated && !a.Offset.IsAnimated)
            {
                // a is not animated.
                if (!b.Start.IsAnimated && !b.Start.IsAnimated && !b.Offset.IsAnimated)
                {
                    // Both are not animated.
                    if (a.Start.InitialValue == b.End.InitialValue)
                    {
                        // a trims out everything. b is unnecessary.
                        return a;
                    }
                    else if (b.Start.InitialValue == b.End.InitialValue)
                    {
                        // b trims out everything. a is unnecessary.
                        return b;
                    }
                    else if (a.Start.InitialValue.Value == 0 && a.End.InitialValue.Value == 1 && a.Offset.InitialValue.Degrees == 0)
                    {
                        // a is trimming nothing. a is unnecessary.
                        return b;
                    }
                    else if (b.Start.InitialValue.Value == 0 && b.End.InitialValue.Value == 1 && b.Offset.InitialValue.Degrees == 0)
                    {
                        // b is trimming nothing. b is unnecessary.
                        return a;
                    }
                }
            }

            Translation.Issues.MultipleTrimPathsIsNotSupported();
            return b;
        }

        /// <summary>
        /// Allow a <see cref="ShapeContext"/> to be used wherever a <see cref="ShapeLayerContext"/> is required.
        /// </summary>
        public static implicit operator ShapeLayerContext(ShapeContext obj) => obj.LayerContext;

        /// <summary>
        /// Allow a <see cref="ShapeContext"/> to be used wherever a <see cref="TranslationContext"/> is required.
        /// </summary>
        public static implicit operator TranslationContext(ShapeContext obj) => obj.LayerContext;
    }
}
