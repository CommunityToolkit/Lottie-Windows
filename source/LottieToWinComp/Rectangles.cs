﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Expressions = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translation for Lottie rectangles.
    /// </summary>
    static class Rectangles
    {
        // NOTES ABOUT RECTANGLE DRAWING AND CORNERS:
        // ==========================================
        // A rectangle can be thought of as having 8 components -
        // 4 sides (1,3,5,7) and 4 corners (2,4,6,8):
        //
        //             1
        //     8╭ ─────────── ╮2
        //      ╷             ╷
        //     7│             │3
        //      ╵             ╵
        //     6╰ ─────────── ╯4
        //             5
        //
        // Windows.Composition draws in order 1,2,3,4,5,6,7,8.
        //
        // Lottie draws in one of two different ways depending on
        // whether the corners are controlled by RoundCorners or by
        // Rectangle.CornerRadius.
        // If RoundCorners the order is 2,3,4,5,6,7,8,1.
        // If Rectangle.CornerRadius the order is 3,4,5,6,7,8,1,2.
        //
        // If the corners have 0 radius, the corners are irrelevant
        // resulting in:
        // Windows.Composition: 1,3,5,7.
        // Lottie:              3,5,7,1.
        //
        // The order of drawing matters only if there is a TrimPath, and in
        // that case:
        // a) If there are no RoundCorners, a TrimOffset equivalent to 90 degrees
        //    must be added.
        // b) If there are RoundCorners, swap width and height, rotate the rectangle
        //    by 90 degrees around the center, and transform the trim path so that
        //    it effectively draws in the reverse direction.
        //
        // TODO - the RoundCorners case with TrimPath is currently not handled correctly
        //        and will cause the trim to appear to be rotated by 90 degrees.
        //
        //
        // Translates a Lottie rectangle to a CompositionShape.
        public static CompositionShape TranslateRectangleContent(ShapeContext context, Rectangle rectangle)
        {
            var result = context.ObjectFactory.CreateSpriteShape();
            var position = Optimizer.TrimAnimatable(context, rectangle.Position);

            if (IsNonRounded(context, rectangle))
            {
                // Non-rounded rectangles are slightly more efficient, but they can only be used
                // if there is no roundness or Round Corners.
                TranslateAndApplyNonRoundedRectangleContent(
                    context,
                    rectangle,
                    position,
                    result);
            }
            else
            {
                TranslateAndApplyRoundedRectangleContent(
                    context,
                    rectangle,
                    position,
                    result);
            }

            return result;
        }

        // Translates a non-rounded Lottie rectangle to a CompositionShape.
        static void TranslateAndApplyNonRoundedRectangleContent(
            ShapeContext context,
            Rectangle rectangle,
            in TrimmedAnimatable<Vector3> position,
            CompositionSpriteShape compositionShape)
        {
            Debug.Assert(IsNonRounded(context, rectangle), "Precondition");

            var geometry = context.ObjectFactory.CreateRectangleGeometry();
            compositionShape.Geometry = geometry;

            var size = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(rectangle.Size);
            if (size is AnimatableXYZ sizeXYZ)
            {
                var width = Optimizer.TrimAnimatable(context, sizeXYZ.X);
                var height = Optimizer.TrimAnimatable(context, sizeXYZ.Y);

                if (!(width.IsAnimated || height.IsAnimated))
                {
                    geometry.Size = ConvertTo.Vector2(width.InitialValue, height.InitialValue);
                }

                geometry.Offset = InitialOffset(width, height, position: position);

                ApplyRectangleContentCommonXY(context, rectangle, compositionShape, width, height, position, geometry);
            }
            else
            {
                var size3 = Optimizer.TrimAnimatable<Vector3>(context, (AnimatableVector3)size);

                if (!size3.IsAnimated)
                {
                    geometry.Size = ConvertTo.Vector2(size3.InitialValue);
                }

                geometry.Offset = InitialOffset(size: size3, position: position);

                ApplyRectangleContentCommon(context, rectangle, compositionShape, size3, position, geometry);
            }
        }

        // Translates a Lottie rectangle to a CompositionShape containing a RoundedRectangle.
        static void TranslateAndApplyRoundedRectangleContent(
            ShapeContext context,
            Rectangle rectangle,
            in TrimmedAnimatable<Vector3> position,
            CompositionSpriteShape compositionShape)
        {
            // Use a rounded rectangle geometry.
            var geometry = context.ObjectFactory.CreateRoundedRectangleGeometry();
            compositionShape.Geometry = geometry;

            // Get the corner radius. This will come from either Rectangle.Roundness
            // or RoundCorners.Radius.
            var cornerRadius = GetCornerRadius(context, rectangle, out var cornerRadiusIsRectangleRoundness);

            // Get the size, converted to an AnimatableXYZ if necessary to handle different easings per channel.
            var size = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(rectangle.Size);
            if (size is AnimatableXYZ sizeXYZ)
            {
                var width = Optimizer.TrimAnimatable(context, sizeXYZ.X);
                var height = Optimizer.TrimAnimatable(context, sizeXYZ.Y);

                ApplyCornerRadius(
                    context,
                    geometry,
                    cornerRadius,
                    initialWidth: width.InitialValue,
                    initialHeight: height.InitialValue,
                    isSizeAnimated: width.IsAnimated || height.IsAnimated,
                    cornerRadiusIsRectangleRoundness: cornerRadiusIsRectangleRoundness);

                geometry.Offset = InitialOffset(width, height, position: position);

                ApplyRectangleContentCommonXY(context, rectangle, compositionShape, width, height, position, geometry);
            }
            else
            {
                var size3 = Optimizer.TrimAnimatable<Vector3>(context, (AnimatableVector3)size);

                ApplyCornerRadius(
                    context,
                    geometry,
                    cornerRadius,
                    initialWidth: size3.InitialValue.X,
                    initialHeight: size3.InitialValue.Y,
                    isSizeAnimated: size3.IsAnimated,
                    cornerRadiusIsRectangleRoundness: cornerRadiusIsRectangleRoundness);

                geometry.Offset = InitialOffset(size: size3, position: position);

                ApplyRectangleContentCommon(context, rectangle, compositionShape, size3, position, geometry);
            }
        }

        static void ApplyCornerRadius(
            ShapeLayerContext context,
            CompositionRoundedRectangleGeometry geometry,
            in TrimmedAnimatable<double> cornerRadius,
            double initialWidth,
            double initialHeight,
            bool isSizeAnimated,
            bool cornerRadiusIsRectangleRoundness)
        {
            var initialSize = ConvertTo.Vector2(initialWidth, initialHeight);

            // In After Effects Rectangle.Roundness and RoundCorners.Radius are clamped to a value
            // that depends on the size of the rectangle.
            // If size or corner radius are animated, handle this with an expression.
            if (cornerRadius.IsAnimated || isSizeAnimated)
            {
                Expressions.Vector2 cornerRadiusExpression;

                if (cornerRadius.IsAnimated)
                {
                    Animate.ScalarPropertySetValue(
                        context,
                        cornerRadius,
                        geometry,
                        cornerRadiusIsRectangleRoundness ? "Roundness" : "Radius");

                    if (isSizeAnimated)
                    {
                        // Both size and cornerRadius are animated.
                        cornerRadiusExpression = cornerRadiusIsRectangleRoundness
                                                        ? ExpressionFactory.RoundessToCornerRadius()
                                                        : ExpressionFactory.RadiusToCornerRadius();
                    }
                    else
                    {
                        // Only the cornerRadius is animated.
                        cornerRadiusExpression = cornerRadiusIsRectangleRoundness
                                                        ? ExpressionFactory.RoundnessToCornerRadius(initialSize)
                                                        : ExpressionFactory.RadiusToCornerRadius(initialSize);
                    }
                }
                else
                {
                    // Only the size is animated.
                    cornerRadiusExpression = cornerRadiusIsRectangleRoundness
                                                        ? ExpressionFactory.RoundnessToCornerRadius(cornerRadius.InitialValue)
                                                        : ExpressionFactory.RadiusToCornerRadius(cornerRadius.InitialValue);
                }

                var cornerRadiusAnimation = context.ObjectFactory.CreateExpressionAnimation(cornerRadiusExpression);
                cornerRadiusAnimation.SetReferenceParameter("my", geometry);
                Animate.WithExpression(geometry, cornerRadiusAnimation, "CornerRadius");
            }
            else
            {
                // Static size and corner radius.
                if (cornerRadiusIsRectangleRoundness)
                {
                    // Rectangle.Roundness corner radius is constrained to half of the smaller side.
                    var cornerRadiusValue = Math.Min(cornerRadius.InitialValue, Math.Min(initialWidth, initialHeight) / 2);
                    geometry.CornerRadius = ConvertTo.Vector2((float)cornerRadiusValue);
                }
                else
                {
                    // RoundCorners corner radii are constrained to half of the coresponding side.
                    geometry.CornerRadius = ConvertTo.Vector2(Math.Min(cornerRadius.InitialValue, initialWidth / 2), Math.Min(cornerRadius.InitialValue, initialHeight / 2));
                }
            }

            if (!isSizeAnimated)
            {
                geometry.Size = initialSize;
            }
        }

        static void ApplyRectangleContentCommon(
            ShapeContext context,
            Rectangle rectangle,
            CompositionSpriteShape compositionRectangle,
            in TrimmedAnimatable<Vector3> size,
            in TrimmedAnimatable<Vector3> position,
            RectangleOrRoundedRectangleGeometry geometry)
        {
            if (compositionRectangle.Geometry is null)
            {
                throw new ArgumentException();
            }

            if (position.IsAnimated || size.IsAnimated)
            {
                Expressions.Vector2 offsetExpression;
                if (position.IsAnimated)
                {
                    Animate.Vector2(context, position, geometry, nameof(Rectangle.Position));
                    geometry.Properties.InsertVector2(nameof(Rectangle.Position), ConvertTo.Vector2(position.InitialValue));
                    if (size.IsAnimated)
                    {
                        // Size AND position are animated.
                        offsetExpression = ExpressionFactory.PositionAndSizeToOffsetExpression;
                        Animate.Vector2(context, size, geometry, nameof(Rectangle.Size));
                    }
                    else
                    {
                        // Only Position is animated
                        offsetExpression = ExpressionFactory.HalfSizeToOffsetExpression(ConvertTo.Vector2(size.InitialValue / 2));
                    }
                }
                else
                {
                    // Only Size is animated.
                    offsetExpression = ExpressionFactory.PositionToOffsetExpression(ConvertTo.Vector2(position.InitialValue));
                    Animate.Vector2(context, size, geometry, nameof(Rectangle.Size));
                }

                var offsetExpressionAnimation = context.ObjectFactory.CreateExpressionAnimation(offsetExpression);
                offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                Animate.WithExpression(geometry, offsetExpressionAnimation, "Offset");
            }

            // Lottie rectangles have 0,0 at top right. That causes problems for TrimPath which expects 0,0 to be top left.
            // Add an offset to the trim path.

            // TODO - this only works correctly if Size and TrimOffset are not animated. A complete solution requires
            //        adding another property.
            var isPartialTrimPath = context.TrimPath != null &&
                (context.TrimPath.Start.IsAnimated || context.TrimPath.End.IsAnimated || context.TrimPath.Offset.IsAnimated ||
                context.TrimPath.Start.InitialValue.Value != 0 || context.TrimPath.End.InitialValue.Value != 1);

            if (size.IsAnimated && isPartialTrimPath)
            {
                // Warn that we might be getting things wrong
                context.Issues.AnimatedRectangleWithTrimPathIsNotSupported();
            }

            var width = size.InitialValue.X;
            var height = size.InitialValue.Y;
            var trimOffsetDegrees = (width / (2 * (width + height))) * 360;

            Shapes.TranslateAndApplyShapeContextWithTrimOffset(
                context,
                compositionRectangle,
                rectangle.DrawingDirection == DrawingDirection.Reverse,
                trimOffsetDegrees: trimOffsetDegrees);

            compositionRectangle.SetDescription(context, () => rectangle.Name);
            compositionRectangle.Geometry.SetDescription(context, () => $"{rectangle.Name}.RectangleGeometry");
        }

        static void ApplyRectangleContentCommonXY(
            ShapeContext context,
            Rectangle rectangle,
            CompositionSpriteShape compositionRectangle,
            in TrimmedAnimatable<double> width,
            in TrimmedAnimatable<double> height,
            in TrimmedAnimatable<Vector3> position,
            RectangleOrRoundedRectangleGeometry geometry)
        {
            if (compositionRectangle.Geometry is null)
            {
                throw new ArgumentException();
            }

            if (position.IsAnimated || width.IsAnimated || height.IsAnimated)
            {
                Expressions.Vector2 offsetExpression;
                if (position.IsAnimated)
                {
                    Animate.Vector2(context, position, geometry, nameof(Rectangle.Position));
                    geometry.Properties.InsertVector2(nameof(Rectangle.Position), ConvertTo.Vector2(position.InitialValue));
                    if (width.IsAnimated || height.IsAnimated)
                    {
                        // Size AND position are animated.
                        offsetExpression = ExpressionFactory.PositionAndSizeToOffsetExpression;
                        if (width.IsAnimated)
                        {
                            Animate.Scalar(context, width, geometry, $"{nameof(Rectangle.Size)}.X");
                        }

                        if (height.IsAnimated)
                        {
                            Animate.Scalar(context, height, geometry, $"{nameof(Rectangle.Size)}.Y");
                        }
                    }
                    else
                    {
                        // Only Position is animated.
                        offsetExpression = ExpressionFactory.HalfSizeToOffsetExpression(ConvertTo.Vector2(new Vector2(width.InitialValue, height.InitialValue) / 2));
                    }
                }
                else
                {
                    // Only Size is animated.
                    offsetExpression = ExpressionFactory.PositionToOffsetExpression(ConvertTo.Vector2(position.InitialValue));
                    if (width.IsAnimated)
                    {
                        Animate.Scalar(context, width, geometry, $"{nameof(Rectangle.Size)}.X");
                    }

                    if (height.IsAnimated)
                    {
                        Animate.Scalar(context, height, geometry, $"{nameof(Rectangle.Size)}.Y");
                    }
                }

                var offsetExpressionAnimation = context.ObjectFactory.CreateExpressionAnimation(offsetExpression);
                offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                Animate.WithExpression(geometry, offsetExpressionAnimation, "Offset");
            }

            // Lottie rectangles have 0,0 at top right. That causes problems for TrimPath which expects 0,0 to be top left.
            // Add an offset to the trim path.

            // TODO - this only works correctly if Size and TrimOffset are not animated. A complete solution requires
            //        adding another property.
            var isPartialTrimPath = context.TrimPath != null &&
                (context.TrimPath.Start.IsAnimated || context.TrimPath.End.IsAnimated || context.TrimPath.Offset.IsAnimated ||
                context.TrimPath.Start.InitialValue.Value != 0 || context.TrimPath.End.InitialValue.Value != 1);

            if ((width.IsAnimated || height.IsAnimated) && isPartialTrimPath)
            {
                // Warn that we might be getting things wrong.
                context.Issues.AnimatedRectangleWithTrimPathIsNotSupported();
            }

            var initialWidth = width.InitialValue;
            var initialHeight = height.InitialValue;
            var trimOffsetDegrees = (initialWidth / (2 * (initialWidth + initialHeight))) * 360;

            Shapes.TranslateAndApplyShapeContextWithTrimOffset(
                context,
                compositionRectangle,
                rectangle.DrawingDirection == DrawingDirection.Reverse,
                trimOffsetDegrees: trimOffsetDegrees);

            compositionRectangle.SetDescription(context, () => rectangle.Name);
            compositionRectangle.Geometry.SetDescription(context, () => $"{rectangle.Name}.RectangleGeometry");
        }

        public static CanvasGeometry CreateWin2dRectangleGeometry(
            ShapeContext context,
            Rectangle rectangle)
        {
            var position = Optimizer.TrimAnimatable(context, rectangle.Position);
            var size = Optimizer.TrimAnimatable(context, rectangle.Size);

            var cornerRadius = GetCornerRadius(context, rectangle, out var cornerRadiusIsRectangleRoundness);

            if (position.IsAnimated || size.IsAnimated || cornerRadius.IsAnimated)
            {
                context.Issues.CombiningAnimatedShapesIsNotSupported();
            }

            var width = size.InitialValue.X;
            var height = size.InitialValue.Y;
            var radiusX = cornerRadius.InitialValue;
            var radiusY = cornerRadius.InitialValue;

            // The radius is treated differently depending on whether it came from Rectangle.Roundness
            // or RoundCorners.Radius.
            if (cornerRadiusIsRectangleRoundness)
            {
                // Radius came from Rectangle.Radius.
                // X and Y have the same radius (the corners are round) which is capped at half
                // the length of the smallest side.
                radiusX = radiusY = Math.Min(Math.Min(width, height) / 2, radiusY);
            }
            else
            {
                // Radius came from RoundCorners.Radius.
                // X and Y radii are capped at half the length of their corresponding sides.
                radiusX = Math.Min(width / 2, radiusX);
                radiusY = Math.Min(width / 2, radiusY);
            }

            var result = CanvasGeometry.CreateRoundedRectangle(
                null,
                (float)(position.InitialValue.X - (width / 2)),
                (float)(position.InitialValue.Y - (height / 2)),
                (float)width,
                (float)height,
                (float)radiusX,
                (float)radiusY);

            var transformMatrix = Transforms.CreateMatrixFromTransform(context, context.Transform);
            if (!transformMatrix.IsIdentity)
            {
                result = result.Transform(transformMatrix);
            }

            result.SetDescription(context, () => rectangle.Name);

            return result;
        }

        // Gets the corner radius and indicates whether the value came from Rectangle.Roundness (as
        // opposed to RoundCorners.Radius).
        static TrimmedAnimatable<double> GetCornerRadius(
            ShapeContext context,
            Rectangle rectangle,
            out bool cornerRadiusIsRectangleRoundness)
        {
            // Choose either Rectangle.Roundness or RoundCorners.Radius to control corner rounding.
            // After Effects ignores RoundCorners.Radius when Rectangle.Roundness is non-0.
            //
            // If Rectangle.Roundness is ever non-0 and RoundCorners.Radius is ever non-0, we'd need to
            // switch between Rectangle.Roundness and RoundCorners.Radius behaviors as RoundCorners.Radius
            // switches between 0 and non-0. That is a rare case and would require a much more complicated
            // set of expressions, so for now we don't support that case.
            //
            // If Rectangle.Roundness is ever non-0, choose it to define the rounding of the corners.
            cornerRadiusIsRectangleRoundness = rectangle.Roundness.IsEverNot(0);

            // If we're using Rectangle.Roundness, check whether that might interfere with the
            // RoundCorners.Radius values.
            if (cornerRadiusIsRectangleRoundness &&
                rectangle.Roundness.IsEver(0) &&
                context.RoundCorners.Radius.IsEverNot(0))
            {
                // Report the issue about RoundCorners being ignored.
                context.Issues.ConflictingRoundnessAndRadiusIsNotSupported();
            }

            return Optimizer.TrimAnimatable(context, cornerRadiusIsRectangleRoundness ? rectangle.Roundness : context.RoundCorners.Radius);
        }

        // Convert the size and position for a geometry into an offset.
        // This is necessary because a geometry's offset describes its
        // top left corner, whereas a Lottie position describes its centerpoint.
        static Sn.Vector2 InitialOffset(
            in TrimmedAnimatable<Vector3> size,
            in TrimmedAnimatable<Vector3> position)
            => ConvertTo.Vector2(position.InitialValue - (size.InitialValue / 2));

        static Sn.Vector2 InitialOffset(
            in TrimmedAnimatable<double> width,
            in TrimmedAnimatable<double> height,
            in TrimmedAnimatable<Vector3> position)
            => ConvertTo.Vector2(position.InitialValue - (new Vector3(width.InitialValue, height.InitialValue, 0) / 2));

        // Returns true if the given rectangle ever has rounded corners.
        static bool IsNonRounded(ShapeContext shapeContext, Rectangle rectangle) =>
            rectangle.Roundness.IsAlways(0) && shapeContext.RoundCorners.Radius.IsAlways(0);
    }
}
