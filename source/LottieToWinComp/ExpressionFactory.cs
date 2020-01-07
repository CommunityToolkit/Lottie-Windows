// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    sealed class ExpressionFactory : Expression
    {
        // The name used to bind to the property set that contains the Progress property.
        const string RootName = "_";
        static readonly Expression s_myTStart = Scalar("my.TStart");
        static readonly Expression s_myTEnd = Scalar("my.TEnd");

        // An expression that refers to the name of the root property set and the Progress property on it.
        internal static readonly Expression RootProgress = Scalar($"{RootName}.{LottieToWinCompTranslator.ProgressPropertyName}");
        internal static readonly Expression MaxTStartTEnd = Max(s_myTStart, s_myTEnd);
        internal static readonly Expression MinTStartTEnd = Min(s_myTStart, s_myTEnd);
        internal static readonly Expression MyColor = Scalar("my.Color");
        internal static readonly Expression MyOpacity = Scalar("my.Opacity");
        internal static readonly Expression MyInheritedOpacity = Scalar("my.InheritedOpacity");
        internal static readonly Expression MyPosition2 = Vector2("my.Position");
        internal static readonly Expression HalfSize2 = Divide(Vector2("my.Size"), Vector2(2, 2));
        internal static readonly Expression AnimatedColorWithAnimatedOpacity =
            Vector4AsColorMultipliedByOpacity(MyColor, MyOpacity);

        internal static readonly Expression AnimatedColorWithAnimatedOpacityWithAnimatedInheritedOpacity =
            Vector4AsColorMultipliedByOpacityByOpacity(MyColor, MyOpacity, MyInheritedOpacity);

        // Depends on MyPosition2 and HalfSize2 so must be declared after them.
        internal static readonly Expression PositionAndSizeToOffsetExpression = Subtract(MyPosition2, HalfSize2);
        internal static readonly Expression TransformMatrixM11Expression = Scalar("my.TransformMatrix._11");
        internal static readonly Expression MyAnchor2 = Vector2("my.Anchor");
        internal static readonly Expression PositionMinusAnchor2 = Subtract(MyPosition2, MyAnchor2);
        internal static readonly Expression MyAnchor3 = Vector3(Scalar("my.Anchor.X"), Scalar("my.Anchor.Y"));
        internal static readonly Expression PositionMinusAnchor3 = Vector3(
                                                                        Subtract(Scalar("my.Position.X"), Scalar("my.Anchor.X")),
                                                                        Subtract(Scalar("my.Position.Y"), Scalar("my.Anchor.Y")),
                                                                        Scalar(0));

        internal static Expression PositionToOffsetExpression(Sn.Vector2 position) => Subtract(Vector2(position), HalfSize2);

        internal static Expression HalfSizeToOffsetExpression(Sn.Vector2 halfSize) => Subtract(MyPosition2, Vector2(halfSize));

        internal static Expression ScaledAndOffsetRootProgress(double scale, double offset)
        {
            var result = RootProgress;

            if (scale != 1)
            {
                result = Multiply(result, Scalar(scale));
            }

            if (offset != 0)
            {
                result = Sum(result, Scalar(offset));
            }

            return result;
        }

        internal static Expression ColorWithAnimatedOpacity(Color color)
            => Vector4AsColorMultipliedByOpacity(Vector4(Scalar(color.R), Scalar(color.G), Scalar(color.B), Scalar(color.A)), MyOpacity);

        // The given color multiplied by MyOpacity, where MyOpacity is pre-multiplied by 255.
        // The premultiplication means that when the color's alpha is 255, after multiplication
        // my MyOpacity simplifies to just (MyOpacity) instead of needing to be (255 * MyOpacity).
        internal static Expression ColorWithPreMultipliedAnimatedOpacity(Color color)
            => Vector4AsColorMultipliedByOpacity(Vector4(Scalar(color.R), Scalar(color.G), Scalar(color.B), Divide(Scalar(color.A), Scalar(255))), MyOpacity);

        internal static Expression BoundColor(string bindingName, double opacity)
            => Vector4AsColorMultipliedByOpacity(RootColor4Property(bindingName), Scalar(opacity));

        internal static Expression BoundColorWithAnimatedOpacity(string bindingName)
            => Vector4AsColorMultipliedByOpacity(RootColor4Property(bindingName), MyOpacity);

        // The value of a Color property stored as a Vector4 on the root.
        static Expression RootColor4Property(string propertyName)
            => Vector4($"{RootName}.{propertyName}");

        static Expression Vector4AsColorMultipliedByOpacity(Expression colorAsVector4, Expression opacity)
            => ColorRGB(
                r: X(colorAsVector4),
                g: Y(colorAsVector4),
                b: Z(colorAsVector4),
                a: Multiply(W(colorAsVector4), opacity));

        static Expression Vector4AsColorMultipliedByOpacityByOpacity(Expression colorAsVector4, Expression opacity, Expression otherOpacity)
            => ColorRGB(
                r: X(colorAsVector4),
                g: Y(colorAsVector4),
                b: Z(colorAsVector4),
                a: Multiply(Multiply(W(colorAsVector4), opacity), otherOpacity));

        ExpressionFactory()
        {
        }

        protected override string CreateExpressionString()
        {
            // Not needed - the class cannot be instantiated.
            throw new NotImplementedException();
        }

        protected override Expression Simplify()
        {
            // Not needed - the class cannot be instantiated.
            throw new NotImplementedException();
        }

        /// <summary>
        /// A segment of a progress expression. Defines the expression that is to be
        /// evaluated between two progress values.
        /// </summary>
        public sealed class Segment
        {
            public Segment(double fromProgress, double toProgress, Expression value)
            {
                Value = value;
                FromProgress = fromProgress;
                ToProgress = toProgress;
            }

            /// <summary>
            /// Gets the values that defines a progress expression over this segment.
            /// </summary>
            public Expression Value { get; }

            public double FromProgress { get; }

            public double ToProgress { get; }
        }

        internal static Expression CreateProgressExpression(Expression progress, params Segment[] segments)
        {
            // Verify that the segments are contiguous and start <= 0 and end >= 1
            var orderedSegments = segments.OrderBy(e => e.FromProgress).ToArray();
            if (orderedSegments.Length == 0)
            {
                throw new ArgumentException();
            }

            double previousTo = orderedSegments[0].FromProgress;
            int? firstSegmentIndex = null;
            int? lastSegmentIndex = null;

            for (var i = 0; i < orderedSegments.Length && !lastSegmentIndex.HasValue; i++)
            {
                var cur = orderedSegments[i];
                if (cur.FromProgress != previousTo)
                {
                    throw new ArgumentException("Progress expression is not contiguous.");
                }

                previousTo = cur.ToProgress;

                // If the segment includes 0, it is the first segment.
                if (!firstSegmentIndex.HasValue)
                {
                    if (cur.FromProgress <= 0 && cur.ToProgress > 0)
                    {
                        firstSegmentIndex = i;
                    }
                }

                // If the segment includes 1, it is the last segment.
                if (!lastSegmentIndex.HasValue)
                {
                    if (cur.ToProgress >= 1)
                    {
                        lastSegmentIndex = i;
                    }
                }
            }

            if (!firstSegmentIndex.HasValue || !lastSegmentIndex.HasValue)
            {
                throw new ArgumentException("Progress expression is not fully defined.");
            }

            // Include only the segments that are >= 0 or <= 1.
            return CreateProgressExpression(
                new ArraySegment<Segment>(
                    array: orderedSegments,
                    offset: firstSegmentIndex.Value,
                    count: 1 + lastSegmentIndex.Value - firstSegmentIndex.Value), progress);
        }

        static Expression CreateProgressExpression(ArraySegment<Segment> segments, Expression progress)
        {
            switch (segments.Count)
            {
                case 0:
                    throw new ArgumentException();
                case 1:
                    return segments.Array[segments.Offset].Value;
                default:
                    // Divide the list of expressions into 2 segments.
                    var pivot = segments.Count / 2;
                    var segmentsArray = segments.Array;
                    var expression0 = CreateProgressExpression(new ArraySegment<Segment>(segmentsArray, segments.Offset, pivot), progress);
                    var expression1 = CreateProgressExpression(new ArraySegment<Segment>(segmentsArray, segments.Offset + pivot, segments.Count - pivot), progress);
                    var pivotProgress = segmentsArray[segments.Offset + pivot - 1].ToProgress;
                    return new Ternary(
                        condition: new LessThan(progress, new Number(pivotProgress)),
                        trueValue: expression0,
                        falseValue: expression1);
            }
        }
    }
}
