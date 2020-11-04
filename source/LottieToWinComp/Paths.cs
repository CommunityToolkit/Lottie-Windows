// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Sn = System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Translates paths.
    /// </summary>
    static class Paths
    {
        // Translates a Lottie PathGeometry to a CompositionSpriteShape.
        public static CompositionSpriteShape TranslatePath(
            LayerContext context,
            in TrimmedAnimatable<PathGeometry> path,
            ShapeFill.PathFillType fillType)
        {
            var result = context.ObjectFactory.CreateSpriteShape();
            var geometry = context.ObjectFactory.CreatePathGeometry();
            result.Geometry = geometry;

            var isPathApplied = false;
            if (path.IsAnimated)
            {
                // In cases where the animated path is just being moved in position we can convert
                // to a static path with an offset animation. This is more efficient because it
                // results in fewer paths, and it works around the inability to support animated
                // paths before version 11.
                if (TryApplyPathAsStaticPathWithAnimatedOffset(context, path, geometry, result, fillType))
                {
                    isPathApplied = true;
                }
                else if (context.ObjectFactory.IsUapApiAvailable(nameof(PathKeyFrameAnimation), versionDependentFeatureDescription: "Path animation"))
                {
                    // PathKeyFrameAnimation was introduced in 6 but was unreliable until 11.
                    Animate.Path(context, path, fillType, geometry, nameof(geometry.Path), nameof(geometry.Path));
                    isPathApplied = true;
                }
            }

            if (!isPathApplied)
            {
                // The Path is not animated, or it is animated but we failed to animate it.
                geometry.Path = Paths.CompositionPathFromPathGeometry(
                    context,
                    path.InitialValue,
                    fillType,
                    optimizeLines: true);
            }

            return result;
        }

        // If the given path is equivalent to a static path with an animated offset, convert
        // the path to that form and apply it to the given geometry and shape.
        static bool TryApplyPathAsStaticPathWithAnimatedOffset(
            LayerContext context,
            in TrimmedAnimatable<PathGeometry> path,
            CompositionPathGeometry geometry,
            CompositionSpriteShape shape,
            ShapeFill.PathFillType fillType)
        {
            Debug.Assert(path.IsAnimated, "Precondition");

            var offsets = new Vector2[path.KeyFrames.Count];
            for (var i = 1; i < path.KeyFrames.Count; i++)
            {
                if (!Paths.TryGetPathTranslation(path.KeyFrames[0].Value.BezierSegments, path.KeyFrames[i].Value.BezierSegments, out offsets[i]))
                {
                    // The animation is not equivalent to a translation.
                    return false;
                }
            }

            // The path is equivalent to a translation. Apply the path described by the initial key frame
            // and apply an offset translation to the CompositionSpriteShape that contains it.
            geometry.Path = Paths.CompositionPathFromPathGeometry(
                context,
                path.InitialValue,
                fillType,
                optimizeLines: true);

            // Create the offsets key frames.
            var keyFrames = new KeyFrame<Vector3>[offsets.Length];

            for (var i = 0; i < path.KeyFrames.Count; i++)
            {
                ref var offset = ref offsets[i];
                var pathKeyFrame = path.KeyFrames[i];
                keyFrames[i] = new KeyFrame<Vector3>(pathKeyFrame.Frame, new Vector3(offset.X, offset.Y, 0), pathKeyFrame.Easing);
            }

            var offsetAnimatable = new TrimmedAnimatable<Vector3>(context, new Vector3(offsets[0].X, offsets[0].Y, 0), keyFrames);

            // Apply the offset animation.
            Animate.Vector2(context, offsetAnimatable, shape, nameof(shape.Offset), "Path animation as a translation.");

            return true;
        }

        public static CompositionShape TranslatePathContent(ShapeContext context, Path path)
        {
            // A path is represented as a SpriteShape with a CompositionPathGeometry.
            var geometry = context.ObjectFactory.CreatePathGeometry();
            geometry.SetDescription(context, () => $"{path.Name}.PathGeometry");

            var pathData = Optimizer.TrimAnimatable(context, Optimizer.GetOptimized(context, path.Data));

            var compositionSpriteShape = TranslatePath(context, pathData, GetPathFillType(context.Fill));
            compositionSpriteShape.SetDescription(context, () => path.Name);

            Shapes.TranslateAndApplyShapeContext(
                context,
                compositionSpriteShape,
                path.DrawingDirection == DrawingDirection.Reverse);

            return compositionSpriteShape;
        }

        /// <summary>
        /// Groups multiple Shapes into a D2D geometry group.
        /// </summary>
        /// <returns>The shape.</returns>
        public static CompositionShape TranslatePathGroupContent(ShapeContext context, IReadOnlyList<Path> paths)
        {
            var groupingSucceeded = PathGeometryGroup.TryGroupPaths(context, paths, out var grouped);

            // If any of the paths have different directions we may not get the translation
            // right, so check that case and warn the user.
            var directions = paths.Select(p => p.DrawingDirection).Distinct().ToArray();

            if (!groupingSucceeded || directions.Length > 1)
            {
                context.Issues.CombiningMultipleAnimatedPathsIsNotSupported();
            }

            // A path is represented as a SpriteShape with a CompositionPathGeometry.
            var compositionPathGeometry = context.ObjectFactory.CreatePathGeometry();

            var compositionSpriteShape = context.ObjectFactory.CreateSpriteShape();
            compositionSpriteShape.Geometry = compositionPathGeometry;

            var pathGroupData = Optimizer.TrimAnimatable(context, grouped);

            ApplyPathGroup(context, compositionPathGeometry, pathGroupData, GetPathFillType(context.Fill));

            if (context.Translation.AddDescriptions)
            {
                var shapeContentName = string.Join("+", paths.Select(sh => sh.Name).Where(a => a != null));
                compositionSpriteShape.SetDescription(context, shapeContentName);
                compositionPathGeometry.SetDescription(context, $"{shapeContentName}.PathGeometry");
            }

            Shapes.TranslateAndApplyShapeContext(
                context,
                compositionSpriteShape,
                reverseDirection: directions[0] == DrawingDirection.Reverse);

            return compositionSpriteShape;
        }

        // Creates a CompositionPath from a single path.
        public static CompositionPath CompositionPathFromPathGeometry(
            TranslationContext context,
            PathGeometry pathGeometry,
            ShapeFill.PathFillType fillType,
            bool optimizeLines)
        {
            var cache = context.GetStateCache<StateCache>();

            // CompositionPaths can be shared by many SpriteShapes so we cache them here.
            // Note that an optimizer that ran over the result could do the same job,
            // but paths are typically very large so it's preferable to cache them here.
            if (!cache.CompositionPaths.TryGetValue((pathGeometry, fillType, optimizeLines), out var result))
            {
                result = new CompositionPath(CreateWin2dPathGeometry(context, pathGeometry, fillType, Sn.Matrix3x2.Identity, optimizeLines));
                cache.CompositionPaths.Add((pathGeometry, fillType, optimizeLines), result);
            }

            return result;
        }

        public static CanvasGeometry CreateWin2dPathGeometryFromShape(
            ShapeContext context,
            Path path,
            ShapeFill.PathFillType fillType,
            bool optimizeLines)
        {
            var pathData = Optimizer.TrimAnimatable(context, path.Data);

            if (pathData.IsAnimated)
            {
                context.Translation.Issues.CombiningAnimatedShapesIsNotSupported();
            }

            var transform = Transforms.CreateMatrixFromTransform(context, context.Transform);

            var result = CreateWin2dPathGeometry(
                context,
                pathData.InitialValue,
                fillType,
                transform,
                optimizeLines: optimizeLines);

            result.SetDescription(context, () => path.Name);

            return result;
        }

        /// <summary>
        /// Creates a CompositionPath from a group of paths.
        /// </summary>
        /// <returns>The <see cref="CompositionPath"/>.</returns>
        public static CompositionPath CompositionPathFromPathGeometryGroup(
            TranslationContext context,
            IEnumerable<PathGeometry> paths,
            ShapeFill.PathFillType fillType,
            bool optimizeLines)
        {
            var compositionPaths = paths.Select(p => CompositionPathFromPathGeometry(context, p, fillType, optimizeLines)).ToArray();

            return compositionPaths.Length == 1
                ? compositionPaths[0]
                : new CompositionPath(
                    CanvasGeometry.CreateGroup(
                        device: null,
                        compositionPaths.Select(p => (CanvasGeometry)p.Source).ToArray(),
                        ConvertTo.FilledRegionDetermination(fillType)));
        }

        public static CanvasGeometry CreateWin2dPathGeometry(
            TranslationContext context,
            PathGeometry figure,
            ShapeFill.PathFillType fillType,
            Sn.Matrix3x2 transformMatrix,
            bool optimizeLines)
        {
            var beziers = figure.BezierSegments;
            using (var builder = new CanvasPathBuilder(null))
            {
                if (beziers.Count == 0)
                {
                    builder.BeginFigure(ConvertTo.Vector2(0));
                    builder.EndFigure(CanvasFigureLoop.Closed);
                }
                else
                {
                    builder.SetFilledRegionDetermination(ConvertTo.FilledRegionDetermination(fillType));
                    builder.BeginFigure(Sn.Vector2.Transform(ConvertTo.Vector2(beziers[0].ControlPoint0), transformMatrix));

                    foreach (var segment in beziers)
                    {
                        var cp0 = Sn.Vector2.Transform(ConvertTo.Vector2(segment.ControlPoint0), transformMatrix);
                        var cp1 = Sn.Vector2.Transform(ConvertTo.Vector2(segment.ControlPoint1), transformMatrix);
                        var cp2 = Sn.Vector2.Transform(ConvertTo.Vector2(segment.ControlPoint2), transformMatrix);
                        var cp3 = Sn.Vector2.Transform(ConvertTo.Vector2(segment.ControlPoint3), transformMatrix);

                        // Add a line rather than a cubic Bezier if the segment is a straight line.
                        if (optimizeLines && segment.IsALine)
                        {
                            // Ignore 0-length lines.
                            if (!cp0.Equals(cp3))
                            {
                                builder.AddLine(cp3);
                            }
                        }
                        else
                        {
                            builder.AddCubicBezier(cp1, cp2, cp3);
                        }
                    }

                    // Closed tells D2D to synthesize a final segment. In many cases Closed
                    // will have no effect because After Effects will have included the final
                    // segment however it can make a difference because it determines whether
                    // mitering or end caps will be used to join the end back to the start.
                    builder.EndFigure(figure.IsClosed ? CanvasFigureLoop.Closed : CanvasFigureLoop.Open);
                }

                return CanvasGeometry.CreatePath(builder);
            } // end using
        }

        /// <summary>
        /// Merges the given paths with MergeMode.Merge.
        /// </summary>
        /// <returns>The merged paths.</returns>
        public static CanvasGeometry MergePaths(CanvasGeometry.Path[] paths)
        {
            Debug.Assert(paths.Length > 1, "Precondition");
            var builder = new CanvasPathBuilder(null);
            var filledRegionDetermination = paths[0].FilledRegionDetermination;
            builder.SetFilledRegionDetermination(filledRegionDetermination);
            foreach (var path in paths)
            {
                Debug.Assert(filledRegionDetermination == path.FilledRegionDetermination, "Invariant");
                foreach (var command in path.Commands)
                {
                    switch (command.Type)
                    {
                        case CanvasPathBuilder.CommandType.BeginFigure:
                            builder.BeginFigure(((CanvasPathBuilder.Command.BeginFigure)command).StartPoint);
                            break;
                        case CanvasPathBuilder.CommandType.EndFigure:
                            builder.EndFigure(((CanvasPathBuilder.Command.EndFigure)command).FigureLoop);
                            break;
                        case CanvasPathBuilder.CommandType.AddCubicBezier:
                            var cb = (CanvasPathBuilder.Command.AddCubicBezier)command;
                            builder.AddCubicBezier(cb.ControlPoint1, cb.ControlPoint2, cb.EndPoint);
                            break;
                        case CanvasPathBuilder.CommandType.AddLine:
                            builder.AddLine(((CanvasPathBuilder.Command.AddLine)command).EndPoint);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }

            return CanvasGeometry.CreatePath(builder);
        }

        /// <summary>
        /// Iff the given paths are offsets translations of each other, gets the translation offset and returns true.
        /// </summary>
        /// <returns><c>true</c> iff the paths are offsets of each other.</returns>
        static bool TryGetPathTranslation(Sequence<BezierSegment> a, Sequence<BezierSegment> b, out Vector2 offset)
        {
            if (a.Count != b.Count)
            {
                // We could never animate this anyway.
                offset = default;
                return false;
            }

            offset = b[0].ControlPoint0 - a[0].ControlPoint0;

            // Compare all of the points in the sequence of beziers to see if they are all offset
            // by the same amount.
            for (var i = 0; i < a.Count; i++)
            {
                var cp0Offset = b[i].ControlPoint0 - a[i].ControlPoint0;
                var cp1Offset = b[i].ControlPoint1 - a[i].ControlPoint1;
                var cp2Offset = b[i].ControlPoint2 - a[i].ControlPoint2;
                var cp3Offset = b[i].ControlPoint3 - a[i].ControlPoint3;

                // Don't compare the values directly - there could be some rounding errors that
                // are acceptable. This value is just a guess about what is acceptable. We could
                // do something a lot more sophisticated (e.g. take into consideration the size
                // of the path) but this is probably good enough.
                const double acceptableError = 0.005;

                if (!IsFuzzyEqual(cp0Offset, offset, acceptableError) ||
                    !IsFuzzyEqual(cp1Offset, offset, acceptableError) ||
                    !IsFuzzyEqual(cp2Offset, offset, acceptableError) ||
                    !IsFuzzyEqual(cp3Offset, offset, acceptableError))
                {
                    offset = default;
                    return false;
                }
            }

            return true;
        }

        static void ApplyPathGroup(
            LayerContext context,
            CompositionPathGeometry targetGeometry,
            in TrimmedAnimatable<PathGeometryGroup> path,
            ShapeFill.PathFillType fillType)
        {
            // PathKeyFrameAnimation was introduced in 6 but was unreliable until 11.
            if (path.IsAnimated && context.ObjectFactory.IsUapApiAvailable(nameof(PathKeyFrameAnimation), versionDependentFeatureDescription: "Path animation"))
            {
                Animate.PathGroup(context, path, fillType, targetGeometry, nameof(targetGeometry.Path), nameof(targetGeometry.Path));
            }
            else
            {
                targetGeometry.Path = CompositionPathFromPathGeometryGroup(
                    context,
                    path.InitialValue.Data,
                    fillType,
                    optimizeLines: true);
            }
        }

        static ShapeFill.PathFillType GetPathFillType(ShapeFill? fill) => fill is null ? ShapeFill.PathFillType.EvenOdd : fill.FillType;

        static bool IsFuzzyEqual(in Vector2 a, in Vector2 b, in double acceptableError)
        {
            var delta = a - b;
            return Math.Abs(delta.X) < acceptableError && Math.Abs(delta.Y) < acceptableError;
        }

        sealed class StateCache
        {
            // Paths are shareable.
            public Dictionary<(PathGeometry, ShapeFill.PathFillType, bool), CompositionPath> CompositionPaths { get; }
                = new Dictionary<(PathGeometry, ShapeFill.PathFillType, bool), CompositionPath>();
        }
    }
}
