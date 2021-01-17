// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Static methods for converting from Lottie types to Composition and CLR types.
    /// </summary>
    static class ConvertTo
    {
        public static WinCompData.Wui.Color Color(Color color) =>
            WinCompData.Wui.Color.FromArgb((byte)(255 * color.A), (byte)(255 * color.R), (byte)(255 * color.G), (byte)(255 * color.B));

        public static Color Color(WinCompData.Wui.Color color) =>
            Animatables.Color.FromArgb(color.A / 255.0, color.R / 255.0, color.G / 255.0, color.B / 255.0);

        public static float Float(double value) => (float)value;

        public static float Float(Trim value) => (float)value.Value;

        public static float Opacity(Opacity value) => (float)value.Value;

        public static float PercentF(double value) => (float)value / 100F;

        public static Sn.Vector2 Vector2(Vector3 vector3) => Vector2(vector3.X, vector3.Y);

        public static Sn.Vector2 Vector2(Vector2 vector2) => Vector2(vector2.X, vector2.Y);

        public static Sn.Vector2 Vector2(double x, double y) => new Sn.Vector2((float)x, (float)y);

        public static Sn.Vector2 Vector2(float x, float y) => new Sn.Vector2(x, y);

        public static Sn.Vector2 Vector2(float x) => new Sn.Vector2(x, x);

        public static Sn.Vector2? Vector2(Sn.Vector2 vector2) => (Sn.Vector2?)vector2;

        public static Sn.Vector2 ClampedVector2(Vector2 vector2) => ClampedVector2((float)vector2.X, (float)vector2.Y);

        public static Sn.Vector2 ClampedVector2(float x, float y) => Vector2(Clamp(x, 0, 1), Clamp(y, 0, 1));

        public static Sn.Vector3 Vector3(double x, double y, double z) => new Sn.Vector3((float)x, (float)y, (float)z);

        public static Sn.Vector3 Vector3(Vector2 vector2) => new Sn.Vector3((float)vector2.X, (float)vector2.Y, 0);

        public static Sn.Vector3 Vector3(Vector3 vector3) => new Sn.Vector3((float)vector3.X, (float)vector3.Y, (float)vector3.Z);

        public static Sn.Vector4 Vector4(WinCompData.Wui.Color color) => new Sn.Vector4(color.R, color.G, color.B, color.A);

        public static WinCompData.Wui.Color Color(Sn.Vector4 color) => WinCompData.Wui.Color.FromArgb((byte)color.W, (byte)color.X, (byte)color.Y, (byte)color.Z);

        static float Clamp(float value, float min, float max)
        {
            Debug.Assert(min <= max, "Precondition");
            return Math.Min(Math.Max(min, value), max);
        }

        public static CompositionStrokeCap? StrokeCapDefaultIsFlat(ShapeStroke.LineCapType lineCapType) =>
            lineCapType switch
            {
                ShapeStroke.LineCapType.Butt => null,
                ShapeStroke.LineCapType.Round => CompositionStrokeCap.Round,
                ShapeStroke.LineCapType.Projected => CompositionStrokeCap.Square,
                _ => throw new InvalidOperationException(),
            };

        public static CompositionStrokeLineJoin? StrokeLineJoinDefaultIsMiter(ShapeStroke.LineJoinType lineJoinType) =>
            lineJoinType switch
            {
                ShapeStroke.LineJoinType.Bevel => CompositionStrokeLineJoin.Bevel,
                ShapeStroke.LineJoinType.Miter => null,
                _ => CompositionStrokeLineJoin.Round,
            };

        public static CanvasFilledRegionDetermination FilledRegionDetermination(ShapeFill.PathFillType fillType)
        {
            return (fillType == ShapeFill.PathFillType.Winding) ? CanvasFilledRegionDetermination.Winding : CanvasFilledRegionDetermination.Alternate;
        }

        public static CanvasGeometryCombine GeometryCombine(MergePaths.MergeMode mergeMode)
        {
            switch (mergeMode)
            {
                case MergePaths.MergeMode.Add: return CanvasGeometryCombine.Union;
                case MergePaths.MergeMode.Subtract: return CanvasGeometryCombine.Exclude;
                case MergePaths.MergeMode.Intersect: return CanvasGeometryCombine.Intersect;

                // TODO - find out what merge should be - maybe should be a Union.
                case MergePaths.MergeMode.Merge:
                case MergePaths.MergeMode.ExcludeIntersections: return CanvasGeometryCombine.Xor;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}