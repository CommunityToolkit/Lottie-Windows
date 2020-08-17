// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using static Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.ExpressionFactory;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    // Translation for Lottie rectangles.
    sealed partial class LottieToWinCompTranslator
    {
        // Translates a Lottie rectangle to a CompositionShape.
        CompositionShape TranslateRectangleContent(TranslationContext context, ShapeContext shapeContext, Rectangle rectangle)
        {
            var result = _c.CreateSpriteShape();
            var position = context.TrimAnimatable(rectangle.Position);

            if (IsNonRounded(shapeContext, rectangle))
            {
                // Non-rounded rectangles are slightly more efficient, but they can only be used
                // if there is no roundness or Round Corners.
                TranslateAndApplyNonRoundedRectangleContent(
                    context,
                    shapeContext,
                    rectangle,
                    position,
                    result);
            }
            else
            {
                TranslateAndApplyRoundedRectangleContent(
                    context,
                    shapeContext,
                    rectangle,
                    position,
                    result);
            }

            return result;
        }

        // Translates a non-rounded Lottie rectangle to a CompositionShape.
        void TranslateAndApplyNonRoundedRectangleContent(
            TranslationContext context,
            ShapeContext shapeContext,
            Rectangle rectangle,
            in TrimmedAnimatable<Vector3> position,
            CompositionSpriteShape compositionShape)
        {
            Debug.Assert(IsNonRounded(shapeContext, rectangle), "Precondition");

            var geometry = _c.CreateRectangleGeometry();
            compositionShape.Geometry = geometry;

            var size = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(rectangle.Size);
            if (size is AnimatableXYZ sizeXYZ)
            {
                var width = context.TrimAnimatable(sizeXYZ.X);
                var height = context.TrimAnimatable(sizeXYZ.Y);

                if (!(width.IsAnimated || height.IsAnimated))
                {
                    geometry.Size = Vector2(width.InitialValue, height.InitialValue);
                }

                geometry.Offset = InitialOffset(width, height, position: position);

                ApplyRectangleContentCommonXY(context, shapeContext, rectangle, compositionShape, width, height, position, geometry);
            }
            else
            {
                var size3 = context.TrimAnimatable<Vector3>((AnimatableVector3)size);

                if (!size3.IsAnimated)
                {
                    geometry.Size = Vector2(size3.InitialValue);
                }

                geometry.Offset = InitialOffset(size: size3, position: position);

                ApplyRectangleContentCommon(context, shapeContext, rectangle, compositionShape, size3, position, geometry);
            }
        }

        // Translates a Lottie rectangle to a CompositionShape containing a RoundedRectangle.
        void TranslateAndApplyRoundedRectangleContent(
            TranslationContext context,
            ShapeContext shapeContext,
            Rectangle rectangle,
            in TrimmedAnimatable<Vector3> position,
            CompositionSpriteShape compositionShape)
        {
            // Use a rounded rectangle geometry.
            var geometry = _c.CreateRoundedRectangleGeometry();
            compositionShape.Geometry = geometry;

            // Get the corner radius. This will come from either Rectangle.Roundness
            // or RoundCorners.Radius.
            var cornerRadius = GetCornerRadius(context, shapeContext, rectangle, out var cornerRadiusIsRectangleRoundness);

            // Get the size, converted to an AnimatableXYZ if necessary to handle different easings per channel.
            var size = AnimatableVector3Rewriter.EnsureOneEasingPerChannel(rectangle.Size);
            if (size is AnimatableXYZ sizeXYZ)
            {
                var width = context.TrimAnimatable(sizeXYZ.X);
                var height = context.TrimAnimatable(sizeXYZ.Y);

                ApplyCornerRadius(
                    context,
                    geometry,
                    cornerRadius,
                    initialWidth: width.InitialValue,
                    initialHeight: height.InitialValue,
                    isSizeAnimated: width.IsAnimated || height.IsAnimated,
                    cornerRadiusIsRectangleRoundness: cornerRadiusIsRectangleRoundness);

                geometry.Offset = InitialOffset(width, height, position: position);

                ApplyRectangleContentCommonXY(context, shapeContext, rectangle, compositionShape, width, height, position, geometry);
            }
            else
            {
                var size3 = context.TrimAnimatable<Vector3>((AnimatableVector3)size);

                ApplyCornerRadius(
                    context,
                    geometry,
                    cornerRadius,
                    initialWidth: size3.InitialValue.X,
                    initialHeight: size3.InitialValue.Y,
                    isSizeAnimated: size3.IsAnimated,
                    cornerRadiusIsRectangleRoundness: cornerRadiusIsRectangleRoundness);

                geometry.Offset = InitialOffset(size: size3, position: position);

                ApplyRectangleContentCommon(context, shapeContext, rectangle, compositionShape, size3, position, geometry);
            }
        }

        void ApplyCornerRadius(
            TranslationContext context,
            CompositionRoundedRectangleGeometry geometry,
            in TrimmedAnimatable<double> cornerRadius,
            double initialWidth,
            double initialHeight,
            bool isSizeAnimated,
            bool cornerRadiusIsRectangleRoundness)
        {
            var initialSize = Vector2(initialWidth, initialHeight);

            // In After Effects Rectangle.Roundness and RoundCorners.Radius are clamped to a value
            // that depends on the size of the rectangle.
            // If size or corner radius are animated, handle this with an expression.
            if (cornerRadius.IsAnimated || isSizeAnimated)
            {
                WinCompData.Expressions.Vector2 cornerRadiusExpression;

                if (cornerRadius.IsAnimated)
                {
                    InsertAndApplyScalarKeyFramePropertySetAnimation(
                        context,
                        cornerRadius,
                        geometry,
                        cornerRadiusIsRectangleRoundness ? "Roundness" : "Radius");

                    if (isSizeAnimated)
                    {
                        // Both size and cornerRadius are animated.
                        cornerRadiusExpression = cornerRadiusIsRectangleRoundness
                                                        ? RoundessToCornerRadius()
                                                        : RadiusToCornerRadius();
                    }
                    else
                    {
                        // Only the cornerRadius is animated.
                        cornerRadiusExpression = cornerRadiusIsRectangleRoundness
                                                        ? RoundnessToCornerRadius(initialSize)
                                                        : RadiusToCornerRadius(initialSize);
                    }
                }
                else
                {
                    // Only the size is animated.
                    cornerRadiusExpression = cornerRadiusIsRectangleRoundness
                                                        ? RoundnessToCornerRadius(cornerRadius.InitialValue)
                                                        : RadiusToCornerRadius(cornerRadius.InitialValue);
                }

                var cornerRadiusAnimation = _c.CreateExpressionAnimation(cornerRadiusExpression);
                cornerRadiusAnimation.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, "CornerRadius", cornerRadiusAnimation);
            }
            else
            {
                // Static size and corner radius.
                if (cornerRadiusIsRectangleRoundness)
                {
                    // Rectangle.Roundness corner radius is constrained to half of the smaller side.
                    var cornerRadiusValue = Math.Min(cornerRadius.InitialValue, Math.Min(initialWidth, initialHeight) / 2);
                    geometry.CornerRadius = Vector2((float)cornerRadiusValue);
                }
                else
                {
                    // RoundCorners corner radii are constrained to half of the coresponding side.
                    geometry.CornerRadius = Vector2(Math.Min(cornerRadius.InitialValue, initialWidth / 2), Math.Min(cornerRadius.InitialValue, initialHeight / 2));
                }
            }

            if (!isSizeAnimated)
            {
                geometry.Size = initialSize;
            }
        }

        void ApplyRectangleContentCommon(
            TranslationContext context,
            ShapeContext shapeContext,
            Rectangle rectangle,
            CompositionSpriteShape compositionRectangle,
            in TrimmedAnimatable<Vector3> size,
            in TrimmedAnimatable<Vector3> position,
            RectangleOrRoundedRectangleGeometry geometry)
        {
            if (position.IsAnimated || size.IsAnimated)
            {
                Expr offsetExpression;
                if (position.IsAnimated)
                {
                    ApplyVector2KeyFrameAnimation(context, position, geometry, nameof(Rectangle.Position));
                    geometry.Properties.InsertVector2(nameof(Rectangle.Position), Vector2(position.InitialValue));
                    if (size.IsAnimated)
                    {
                        // Size AND position are animated.
                        offsetExpression = ExpressionFactory.PositionAndSizeToOffsetExpression;
                        ApplyVector2KeyFrameAnimation(context, size, geometry, nameof(Rectangle.Size));
                    }
                    else
                    {
                        // Only Position is animated
                        offsetExpression = ExpressionFactory.HalfSizeToOffsetExpression(Vector2(size.InitialValue / 2));
                    }
                }
                else
                {
                    // Only Size is animated.
                    offsetExpression = ExpressionFactory.PositionToOffsetExpression(Vector2(position.InitialValue));
                    ApplyVector2KeyFrameAnimation(context, size, geometry, nameof(Rectangle.Size));
                }

                var offsetExpressionAnimation = _c.CreateExpressionAnimation(offsetExpression);
                offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, "Offset", offsetExpressionAnimation);
            }

            // Lottie rectangles have 0,0 at top right. That causes problems for TrimPath which expects 0,0 to be top left.
            // Add an offset to the trim path.

            // TODO - this only works correctly if Size and TrimOffset are not animated. A complete solution requires
            //        adding another property.
            var isPartialTrimPath = shapeContext.TrimPath != null &&
                (shapeContext.TrimPath.Start.IsAnimated || shapeContext.TrimPath.End.IsAnimated || shapeContext.TrimPath.Offset.IsAnimated ||
                shapeContext.TrimPath.Start.InitialValue.Value != 0 || shapeContext.TrimPath.End.InitialValue.Value != 1);

            if (size.IsAnimated && isPartialTrimPath)
            {
                // Warn that we might be getting things wrong
                _issues.AnimatedRectangleWithTrimPathIsNotSupported();
            }

            var width = size.InitialValue.X;
            var height = size.InitialValue.Y;
            var trimOffsetDegrees = (width / (2 * (width + height))) * 360;

            TranslateAndApplyShapeContext(
                context,
                shapeContext,
                compositionRectangle,
                rectangle.DrawingDirection == DrawingDirection.Reverse,
                trimOffsetDegrees: trimOffsetDegrees);

            if (_addDescriptions)
            {
                Describe(compositionRectangle, rectangle.Name);
                Describe(compositionRectangle.Geometry, $"{rectangle.Name}.RectangleGeometry");
            }
        }

        void ApplyRectangleContentCommonXY(
            TranslationContext context,
            ShapeContext shapeContext,
            Rectangle rectangle,
            CompositionSpriteShape compositionRectangle,
            in TrimmedAnimatable<double> width,
            in TrimmedAnimatable<double> height,
            in TrimmedAnimatable<Vector3> position,
            RectangleOrRoundedRectangleGeometry geometry)
        {
            if (position.IsAnimated || width.IsAnimated || height.IsAnimated)
            {
                Expr offsetExpression;
                if (position.IsAnimated)
                {
                    ApplyVector2KeyFrameAnimation(context, position, geometry, nameof(Rectangle.Position));
                    geometry.Properties.InsertVector2(nameof(Rectangle.Position), Vector2(position.InitialValue));
                    if (width.IsAnimated || height.IsAnimated)
                    {
                        // Size AND position are animated.
                        offsetExpression = ExpressionFactory.PositionAndSizeToOffsetExpression;
                        if (width.IsAnimated)
                        {
                            ApplyScalarKeyFrameAnimation(context, width, geometry, $"{nameof(Rectangle.Size)}.X");
                        }

                        if (height.IsAnimated)
                        {
                            ApplyScalarKeyFrameAnimation(context, height, geometry, $"{nameof(Rectangle.Size)}.Y");
                        }
                    }
                    else
                    {
                        // Only Position is animated.
                        offsetExpression = ExpressionFactory.HalfSizeToOffsetExpression(Vector2(new Vector2(width.InitialValue, height.InitialValue) / 2));
                    }
                }
                else
                {
                    // Only Size is animated.
                    offsetExpression = ExpressionFactory.PositionToOffsetExpression(Vector2(position.InitialValue));
                    if (width.IsAnimated)
                    {
                        ApplyScalarKeyFrameAnimation(context, width, geometry, $"{nameof(Rectangle.Size)}.X");
                    }

                    if (height.IsAnimated)
                    {
                        ApplyScalarKeyFrameAnimation(context, height, geometry, $"{nameof(Rectangle.Size)}.Y");
                    }
                }

                var offsetExpressionAnimation = _c.CreateExpressionAnimation(offsetExpression);
                offsetExpressionAnimation.SetReferenceParameter("my", geometry);
                StartExpressionAnimation(geometry, "Offset", offsetExpressionAnimation);
            }

            // Lottie rectangles have 0,0 at top right. That causes problems for TrimPath which expects 0,0 to be top left.
            // Add an offset to the trim path.

            // TODO - this only works correctly if Size and TrimOffset are not animated. A complete solution requires
            //        adding another property.
            var isPartialTrimPath = shapeContext.TrimPath != null &&
                (shapeContext.TrimPath.Start.IsAnimated || shapeContext.TrimPath.End.IsAnimated || shapeContext.TrimPath.Offset.IsAnimated ||
                shapeContext.TrimPath.Start.InitialValue.Value != 0 || shapeContext.TrimPath.End.InitialValue.Value != 1);

            if ((width.IsAnimated || height.IsAnimated) && isPartialTrimPath)
            {
                // Warn that we might be getting things wrong.
                _issues.AnimatedRectangleWithTrimPathIsNotSupported();
            }

            var initialWidth = width.InitialValue;
            var initialHeight = height.InitialValue;
            var trimOffsetDegrees = (initialWidth / (2 * (initialWidth + initialHeight))) * 360;

            TranslateAndApplyShapeContext(
                context,
                shapeContext,
                compositionRectangle,
                rectangle.DrawingDirection == DrawingDirection.Reverse,
                trimOffsetDegrees: trimOffsetDegrees);

            if (_addDescriptions)
            {
                Describe(compositionRectangle, rectangle.Name);
                Describe(compositionRectangle.Geometry, $"{rectangle.Name}.RectangleGeometry");
            }
        }

        CanvasGeometry CreateWin2dRectangleGeometry(
            TranslationContext context,
            ShapeContext shapeContext,
            Rectangle rectangle)
        {
            var position = context.TrimAnimatable(rectangle.Position);
            var size = context.TrimAnimatable(rectangle.Size);

            var cornerRadius = GetCornerRadius(context, shapeContext, rectangle, out var cornerRadiusIsRectangleRoundness);

            if (position.IsAnimated || size.IsAnimated || cornerRadius.IsAnimated)
            {
                _issues.CombiningAnimatedShapesIsNotSupported();
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

            var transformMatrix = CreateMatrixFromTransform(context, shapeContext.Transform);
            if (!transformMatrix.IsIdentity)
            {
                result = result.Transform(transformMatrix);
            }

            if (_addDescriptions)
            {
                Describe(result, rectangle.Name);
            }

            return result;
        }

        // Gets the corner radius and indicates whether the value came from Rectangle.Roundness (as
        // opposed to RoundCorners.Radius).
        TrimmedAnimatable<double> GetCornerRadius(
            TranslationContext context,
            ShapeContext shapeContext,
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
                shapeContext.RoundCorners.Radius.IsEverNot(0))
            {
                // Report the issue about RoundCorners being ignored.
                _issues.ConflictingRoundnessAndRadiusIsNotSupported();
            }

            return context.TrimAnimatable(cornerRadiusIsRectangleRoundness ? rectangle.Roundness : shapeContext.RoundCorners.Radius);
        }

        // Convert the size and position for a geometry into an offset.
        // This is necessary because a geometry's offset describes its
        // top left corner, whereas a Lottie position describes its centerpoint.
        static Sn.Vector2 InitialOffset(
            in TrimmedAnimatable<Vector3> size,
            in TrimmedAnimatable<Vector3> position)
            => Vector2(position.InitialValue - (size.InitialValue / 2));

        static Sn.Vector2 InitialOffset(
            in TrimmedAnimatable<double> width,
            in TrimmedAnimatable<double> height,
            in TrimmedAnimatable<Vector3> position)
            => Vector2(position.InitialValue - (new Vector3(width.InitialValue, height.InitialValue, 0) / 2));

        // Returns true if the given rectangle ever has rounded corners.
        static bool IsNonRounded(ShapeContext shapeContext, Rectangle rectangle) =>
            rectangle.Roundness.IsAlways(0) && shapeContext.RoundCorners.Radius.IsAlways(0);
    }
}
