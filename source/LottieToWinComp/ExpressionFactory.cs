// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;
using static Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions.Expression;
using Sn = System.Numerics;
using Wui = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    static class ExpressionFactory
    {
        // The name used to bind to the property set that contains the Progress property.
        internal const string RootName = "_";

        // The name used to bind to the property set that contains the theme properties.
        internal const string ThemePropertiesName = "_theme";

        internal static readonly Vector2 MyAnchor = MyVector2("Anchor");
        internal static readonly Vector3 MyAnchor3 = Vector3(MyAnchor.X, MyAnchor.Y, 0);
        internal static readonly Vector4 MyColor = MyVector4("Color");
        internal static readonly Scalar MyInheritedOpacity = MyScalar("InheritedOpacity");
        internal static readonly Scalar MyOpacity = MyScalar("Opacity");
        internal static readonly Vector2 MyPosition = MyVector2("Position");
        internal static readonly Vector2 MySize = MyVector2("Size");
        internal static readonly Matrix3x2 MyTransformMatrix = MyMatrix3x2("TransformMatrix");
        static readonly Scalar MyTStart = MyScalar("TStart");
        static readonly Scalar MyTEnd = MyScalar("TEnd");

        // An expression that refers to the name of the root property set and the Progress property on it.
        internal static readonly Scalar RootProgress = RootScalar(LottieToWinCompTranslator.ProgressPropertyName);
        internal static readonly Scalar MaxTStartTEnd = Max(MyTStart, MyTEnd);
        internal static readonly Scalar MinTStartTEnd = Min(MyTStart, MyTEnd);
        static readonly Vector2 HalfMySize = MySize / Vector2(2, 2);
        internal static readonly Color AnimatedColorWithAnimatedOpacity =
            ColorAsVector4MultipliedByOpacities(MyColor, new[] { MyOpacity });

        // Depends on MyPosition2 and HalfSize2 so must be declared after them.
        internal static readonly Vector2 PositionAndSizeToOffsetExpression = MyPosition - HalfMySize;
        internal static readonly Scalar TransformMatrixM11Expression = MyTransformMatrix._11;
        internal static readonly Vector2 PositionMinusAnchor2 = MyPosition - MyAnchor;
        internal static readonly Vector3 PositionMinusAnchor3 = Vector3(
                                                                        MyPosition.X - MyAnchor.X,
                                                                        MyPosition.Y - MyAnchor.Y,
                                                                        0);

        internal static Color ThemedColorMultipliedByOpacity(string bindingName, LottieData.Opacity opacity)
            => ColorAsVector4MultipliedByOpacity(ThemedColor4Property(bindingName), opacity.Value);

        internal static Color ThemedColorAsVector4MultipliedByOpacities(string bindingName, Scalar[] opacities)
            => ColorAsVector4MultipliedByOpacities(ThemedColor4Property(bindingName), opacities);

        internal static Scalar ThemedScalar(string bindingName) => Scalar(ThemeProperty(bindingName));

        // The given color multiplied by the given opacity, where the opacity is pre-multiplied by 255.
        // The premultiplication can result in a simpler expression when color.A is 255 because
        // 255 / 255 * premultipliedOpacity * 255 will simplify to just premultipliedOpacity.
        internal static Color ColorMultipliedByPreMultipliedOpacities(Wui.Color color, Scalar[] premultipliedOpacities)
            => ColorAsVector4MultipliedByOpacities(Vector4(color.R, color.G, color.B, color.A / 255.0), premultipliedOpacities);

        internal static Vector2 HalfSizeToOffsetExpression(Sn.Vector2 halfSize) => MyPosition - Vector2(halfSize);

        internal static Vector2 PositionToOffsetExpression(Sn.Vector2 position) => Vector2(position) - HalfMySize;

        internal static Scalar RootScalar(string propertyName) => Scalar(RootProperty(propertyName));

        // The value of a Color property stored as a Vector4 on the theming property set.
        static Vector4 ThemedColor4Property(string propertyName) => Vector4(ThemeProperty(propertyName));

        internal static Scalar ScaledAndOffsetRootProgress(double scale, double offset)
        {
            var result = RootProgress;

            // Avoid creating expressions that are more complex than necessary.
            // Even though they'll simplify down, they create unnecessary objects.
            if (scale != 1)
            {
                result *= scale;
            }

            if (offset != 0)
            {
                result += offset;
            }

            return result;
        }

        internal static Color MyColorAsVector4MultipliedByOpacity(Scalar[] opacities)
            => ColorAsVector4MultipliedByOpacities(MyColor, opacities);

        static Color ColorAsVector4MultipliedByOpacity(Vector4 colorAsVector4, Scalar opacity)
            => Color(
                r: colorAsVector4.X,
                g: colorAsVector4.Y,
                b: colorAsVector4.Z,
                a: colorAsVector4.W * opacity);

        static Color ColorAsVector4MultipliedByOpacities(Vector4 colorAsVector4, Scalar[] opacities)
        {
            var multipliedOpacities = opacities[0];
            for (var i = 1; i < opacities.Length; i++)
            {
                multipliedOpacities *= opacities[i];
            }

            return ColorAsVector4MultipliedByOpacity(colorAsVector4, multipliedOpacities);
        }

        /// <summary>
        /// A segment of a progress expression. Defines the expression that is to be
        /// evaluated between two progress values.
        /// </summary>
        public sealed class Segment
        {
            public Segment(double fromProgress, double toProgress, Scalar value)
            {
                Value = value;
                FromProgress = fromProgress;
                ToProgress = toProgress;
            }

            /// <summary>
            /// Gets the values that defines a progress expression over this segment.
            /// </summary>
            public Scalar Value { get; }

            public double FromProgress { get; }

            public double ToProgress { get; }
        }

        internal static Scalar CreateProgressExpression(Scalar progress, params Segment[] segments)
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

        static Scalar CreateProgressExpression(ArraySegment<Segment> segments, Scalar progress)
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
                    return Ternary(
                        condition: LessThan(progress, pivotProgress),
                        trueValue: expression0,
                        falseValue: expression1);
            }
        }

        static Matrix3x2 MyMatrix3x2(string propertyName) => Matrix3x2(My(propertyName));

        static Scalar MyScalar(string propertyName) => Scalar(My(propertyName));

        static Vector2 MyVector2(string propertyName) => Vector2(My(propertyName));

        static Vector4 MyVector4(string propertyName) => Vector4(My(propertyName));

        static string My(string propertyName) => $"my.{propertyName}";

        // A property on the root property set. Used to bind to the property set that contains the Progress property.
        static string RootProperty(string propertyName) => $"{RootName}.{propertyName}";

        // An property on the theming property set. Used to bind to properties that can be
        // updated for theming purposes.
        static string ThemeProperty(string propertyName) => $"{ThemePropertiesName}.{propertyName}";
    }
}
