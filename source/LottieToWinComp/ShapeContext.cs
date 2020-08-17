// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Describes the environment in which a Lottie shape should be interpreted.
    /// </summary>
    sealed class ShapeContext
    {
        // A RoundCorners with a radius of 0.
        static readonly RoundCorners s_defaultRoundCorners =
            new RoundCorners(new ShapeLayerContent.ShapeLayerContentArgs { }, new Animatable<double>(0, null));

        readonly TranslationIssues _issues;

        internal ShapeContext(TranslationIssues issues) => _issues = issues;

        internal ShapeStroke Stroke { get; private set; }

        internal ShapeFill Fill { get; private set; }

        internal TrimPath TrimPath { get; private set; }

        /// <summary>
        /// Never null. If there is no <see cref="RoundCorners"/> set, a default
        /// 0 <see cref="RoundCorners"/> will be returned.
        /// </summary>
        internal RoundCorners RoundCorners { get; private set; } = s_defaultRoundCorners;

        internal Transform Transform { get; private set; }

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

        internal void UpdateOpacityFromTransform(TranslationContext context, Transform transform)
        {
            if (transform is null)
            {
                return;
            }

            Opacity = Opacity.ComposedWith(context.TrimAnimatable(transform.Opacity));
        }

        // Only used when translating geometries. Layers use an extra Shape or Visual to
        // apply the transform, but geometries need to take the transform into account when
        // they're created.
        internal void SetTransform(Transform transform)
        {
            Transform = transform;
        }

        internal ShapeContext Clone() =>
            new ShapeContext(_issues)
            {
                Fill = Fill,
                Stroke = Stroke,
                TrimPath = TrimPath,
                RoundCorners = RoundCorners,
                Opacity = Opacity,
                Transform = Transform,
            };

        ShapeFill ComposeFills(ShapeFill a, ShapeFill b)
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
                _issues.MultipleFillsIsNotSupported();
                return b;
            }

            switch (a.FillKind)
            {
                case ShapeFill.ShapeFillKind.SolidColor:
                    return ComposeSolidColorFills((SolidColorFill)a, (SolidColorFill)b);
            }

            _issues.MultipleFillsIsNotSupported();
            return b;
        }

        SolidColorFill ComposeSolidColorFills(SolidColorFill a, SolidColorFill b)
        {
            if (!b.Color.IsAnimated && !b.Opacity.IsAnimated)
            {
                if (b.Opacity.InitialValue == LottieData.Opacity.Opaque &&
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

            _issues.MultipleFillsIsNotSupported();
            return b;
        }

        ShapeStroke ComposeStrokes(ShapeStroke a, ShapeStroke b)
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
                _issues.MultipleStrokesIsNotSupported();
                return b;
            }

            switch (a.StrokeKind)
            {
                case ShapeStroke.ShapeStrokeKind.SolidColor:
                    return ComposeSolidColorStrokes((SolidColorStroke)a, (SolidColorStroke)b);
                case ShapeStroke.ShapeStrokeKind.LinearGradient:
                    return ComposeLinearGradientStrokes((LinearGradientStroke)a, (LinearGradientStroke)b);
                case ShapeStroke.ShapeStrokeKind.RadialGradient:
                    return ComposeRadialGradientStrokes((RadialGradientStroke)a, (RadialGradientStroke)b);
                default:
                    throw new InvalidOperationException();
            }
        }

        LinearGradientStroke ComposeLinearGradientStrokes(LinearGradientStroke a, LinearGradientStroke b)
        {
            Debug.Assert(a != null && b != null, "Precondition");

            if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                a.Opacity.IsAlways(LottieData.Opacity.Opaque) && b.Opacity.IsAlways(LottieData.Opacity.Opaque))
            {
                if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                {
                    // a occludes b, so b can be ignored.
                    return a;
                }
            }

            _issues.MultipleStrokesIsNotSupported();
            return a;
        }

        RadialGradientStroke ComposeRadialGradientStrokes(RadialGradientStroke a, RadialGradientStroke b)
        {
            Debug.Assert(a != null && b != null, "Precondition");

            if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                a.Opacity.IsAlways(LottieData.Opacity.Opaque) && b.Opacity.IsAlways(LottieData.Opacity.Opaque))
            {
                if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                {
                    // a occludes b, so b can be ignored.
                    return a;
                }
            }

            _issues.MultipleStrokesIsNotSupported();
            return a;
        }

        SolidColorStroke ComposeSolidColorStrokes(SolidColorStroke a, SolidColorStroke b)
        {
            Debug.Assert(a != null && b != null, "Precondition");

            if (!a.StrokeWidth.IsAnimated && !b.StrokeWidth.IsAnimated &&
                !a.DashPattern.Any() && !b.DashPattern.Any() &&
                a.Opacity.IsAlways(LottieData.Opacity.Opaque) && b.Opacity.IsAlways(LottieData.Opacity.Opaque))
            {
                if (a.StrokeWidth.InitialValue >= b.StrokeWidth.InitialValue)
                {
                    // a occludes b, so b can be ignored.
                    return a;
                }
            }

            // The new stroke should be in addition to the existing stroke. And colors should blend.
            _issues.MultipleStrokesIsNotSupported();
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

            _issues.MultipleAnimatedRoundCornersIsNotSupported();
            return b;
        }

        TrimPath ComposeTrimPaths(TrimPath a, TrimPath b)
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

            _issues.MultipleTrimPathsIsNotSupported();
            return b;
        }
    }
}
