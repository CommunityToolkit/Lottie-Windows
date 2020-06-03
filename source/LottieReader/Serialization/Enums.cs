// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        BlendMode BmToBlendMode(double? bm)
        {
            if (bm.HasValue)
            {
                if (TryAsExactInt(bm.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 0: return BlendMode.Normal;
                        case 1: return BlendMode.Multiply;
                        case 2: return BlendMode.Screen;
                        case 3: return BlendMode.Overlay;
                        case 4: return BlendMode.Darken;
                        case 5: return BlendMode.Lighten;
                        case 6: return BlendMode.ColorDodge;
                        case 7: return BlendMode.ColorBurn;
                        case 8: return BlendMode.HardLight;
                        case 9: return BlendMode.SoftLight;
                        case 10: return BlendMode.Difference;
                        case 11: return BlendMode.Exclusion;
                        case 12: return BlendMode.Hue;
                        case 13: return BlendMode.Saturation;
                        case 14: return BlendMode.Color;
                        case 15: return BlendMode.Luminosity;
                    }
                }

                _issues.UnexpectedValueForType("BlendMode", bm.ToString());
            }

            return BlendMode.Normal;
        }

        DrawingDirection DToDrawingDirection(double? d)
        {
            if (d.HasValue)
            {
                if (TryAsExactInt(d.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return DrawingDirection.Forward;
                        case 3: return DrawingDirection.Reverse;
                    }
                }

                _issues.UnexpectedValueForType("DrawingDirection", d.ToString());
            }

            return DrawingDirection.Forward;
        }

        ShapeStroke.LineCapType LcToLineCapType(double? lc)
        {
            if (lc.HasValue)
            {
                if (TryAsExactInt(lc.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return ShapeStroke.LineCapType.Butt;
                        case 2: return ShapeStroke.LineCapType.Round;
                        case 3: return ShapeStroke.LineCapType.Projected;
                    }
                }

                _issues.UnexpectedValueForType("LineCapType", lc.ToString());
            }

            return ShapeStroke.LineCapType.Butt;
        }

        ShapeStroke.LineJoinType LjToLineJoinType(double? lj)
        {
            if (lj.HasValue)
            {
                if (TryAsExactInt(lj.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return ShapeStroke.LineJoinType.Miter;
                        case 2: return ShapeStroke.LineJoinType.Round;
                        case 3: return ShapeStroke.LineJoinType.Bevel;
                    }
                }

                _issues.UnexpectedValueForType("LineJoinType", lj.ToString());
            }

            return ShapeStroke.LineJoinType.Miter;
        }

        MergePaths.MergeMode MmToMergeMode(double? mm)
        {
            if (mm.HasValue)
            {
                if (TryAsExactInt(mm.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return MergePaths.MergeMode.Merge;
                        case 2: return MergePaths.MergeMode.Add;
                        case 3: return MergePaths.MergeMode.Subtract;
                        case 4: return MergePaths.MergeMode.Intersect;
                        case 5: return MergePaths.MergeMode.ExcludeIntersections;
                    }
                }

                _issues.UnexpectedValueForType("MergeMode", mm.ToString());
            }

            return MergePaths.MergeMode.Merge;
        }

        TrimPath.TrimType MToTrimType(double? m)
        {
            if (m.HasValue)
            {
                if (TryAsExactInt(m.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return TrimPath.TrimType.Simultaneously;
                        case 2: return TrimPath.TrimType.Individually;
                    }
                }

                _issues.UnexpectedValueForType("TrimType", m.ToString());
            }

            return TrimPath.TrimType.Simultaneously;
        }

        Polystar.PolyStarType? SyToPolystarType(double? sy)
        {
            if (sy.HasValue)
            {
                if (TryAsExactInt(sy.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return Polystar.PolyStarType.Star;
                        case 2: return Polystar.PolyStarType.Polygon;
                    }
                }

                _issues.UnexpectedValueForType("PolyStartType", sy.ToString());
            }

            return null;
        }

        GradientType TToGradientType(double? t)
        {
            if (t.HasValue)
            {
                if (TryAsExactInt(t.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 1: return GradientType.Linear;
                        case 2: return GradientType.Radial;
                    }
                }

                _issues.UnexpectedValueForType("GradientType", t.ToString());
            }

            return GradientType.Linear;
        }

        Layer.MatteType TTToMatteType(double? tt)
        {
            if (tt.HasValue)
            {
                if (TryAsExactInt(tt.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 0: return Layer.MatteType.None;
                        case 1: return Layer.MatteType.Add;
                        case 2: return Layer.MatteType.Invert;
                    }
                }

                _issues.UnexpectedValueForType("MatteType", tt.ToString());
            }

            return Layer.MatteType.None;
        }

        Layer.LayerType? TyToLayerType(double? ty)
        {
            if (ty.HasValue)
            {
                if (TryAsExactInt(ty.Value, out var intValue))
                {
                    switch (intValue)
                    {
                        case 0: return Layer.LayerType.PreComp;
                        case 1: return Layer.LayerType.Solid;
                        case 2: return Layer.LayerType.Image;
                        case 3: return Layer.LayerType.Null;
                        case 4: return Layer.LayerType.Shape;
                        case 5: return Layer.LayerType.Text;
                    }
                }

                _issues.UnexpectedValueForType("LayerType", ty.ToString());
            }

            return null;
        }

        static bool TryAsExactInt(double value, out int intValue)
        {
            intValue = (int)value;
            return value == intValue;
        }

        enum GradientType
        {
            Linear,
            Radial,
        }
    }
}
