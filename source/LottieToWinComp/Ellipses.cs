// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Optimization;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates ellipses to <see cref="CompositionEllipseGeometry"/> and Win2D paths.
    /// </summary>
    static class Ellipses
    {
        public static CompositionShape TranslateEllipseContent(ShapeContext context, Ellipse shapeContent)
        {
            // An ellipse is represented as a SpriteShape with a CompositionEllipseGeometry.
            var compositionSpriteShape = context.ObjectFactory.CreateSpriteShape();
            compositionSpriteShape.SetDescription(context, () => shapeContent.Name);

            var compositionEllipseGeometry = context.ObjectFactory.CreateEllipseGeometry();
            compositionEllipseGeometry.SetDescription(context, () => $"{shapeContent.Name}.EllipseGeometry");

            compositionSpriteShape.Geometry = compositionEllipseGeometry;

            var position = Optimizer.TrimAnimatable(context, shapeContent.Position);
            if (position.IsAnimated)
            {
                Animate.Vector2(context, position, compositionEllipseGeometry, "Center");
            }
            else
            {
                compositionEllipseGeometry.Center = ConvertTo.Vector2(position.InitialValue);
            }

            // Ensure that the diameter is expressed in a form that has only one easing per channel.
            var diameter = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(shapeContent.Diameter);
            if (diameter is AnimatableXYZ diameterXYZ)
            {
                var diameterX = Optimizer.TrimAnimatable(context, diameterXYZ.X);
                var diameterY = Optimizer.TrimAnimatable(context, diameterXYZ.Y);
                if (diameterX.IsAnimated)
                {
                    Animate.ScaledScalar(context, diameterX, 0.5, compositionEllipseGeometry, $"{nameof(CompositionEllipseGeometry.Radius)}.X");
                }

                if (diameterY.IsAnimated)
                {
                    Animate.ScaledScalar(context, diameterY, 0.5, compositionEllipseGeometry, $"{nameof(CompositionEllipseGeometry.Radius)}.Y");
                }

                if (!diameterX.IsAnimated || !diameterY.IsAnimated)
                {
                    compositionEllipseGeometry.Radius = ConvertTo.Vector2(diameter.InitialValue) * 0.5F;
                }
            }
            else
            {
                var diameter3 = Optimizer.TrimAnimatable<Vector3>(context, (AnimatableVector3)diameter);
                if (diameter3.IsAnimated)
                {
                    Animate.ScaledVector2(context, diameter3, 0.5, compositionEllipseGeometry, nameof(CompositionEllipseGeometry.Radius));
                }
                else
                {
                    compositionEllipseGeometry.Radius = ConvertTo.Vector2(diameter.InitialValue) * 0.5F;
                }
            }

            Shapes.TranslateAndApplyShapeContext(
                context,
                compositionSpriteShape,
                reverseDirection: shapeContent.DrawingDirection == DrawingDirection.Reverse);

            return compositionSpriteShape;
        }

        public static CanvasGeometry CreateWin2dEllipseGeometry(ShapeContext context, Ellipse ellipse)
        {
            var ellipsePosition = Optimizer.TrimAnimatable(context, ellipse.Position);
            var ellipseDiameter = Optimizer.TrimAnimatable(context, ellipse.Diameter);

            if (ellipsePosition.IsAnimated || ellipseDiameter.IsAnimated)
            {
                context.Translation.Issues.CombiningAnimatedShapesIsNotSupported();
            }

            var xRadius = ellipseDiameter.InitialValue.X / 2;
            var yRadius = ellipseDiameter.InitialValue.Y / 2;

            var result = CanvasGeometry.CreateEllipse(
                null,
                (float)(ellipsePosition.InitialValue.X - (xRadius / 2)),
                (float)(ellipsePosition.InitialValue.Y - (yRadius / 2)),
                (float)xRadius,
                (float)yRadius);

            var transformMatrix = Transforms.CreateMatrixFromTransform(context, context.Transform);
            if (!transformMatrix.IsIdentity)
            {
                result = result.Transform(transformMatrix);
            }

            result.SetDescription(context, () => ellipse.Name);

            return result;
        }
    }
}
