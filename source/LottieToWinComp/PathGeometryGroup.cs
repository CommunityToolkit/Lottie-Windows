// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// A group of <see cref="PathGeometry"/>.
    /// </summary>
    sealed class PathGeometryGroup : IEquatable<PathGeometryGroup>
    {
        PathGeometryGroup(PathGeometry[] data, Easing easing)
        {
            Data = data;
            Easing = easing;
        }

        public Easing Easing { get; }

        /// <summary>
        /// The geometries in the group.
        /// </summary>
        public IReadOnlyList<PathGeometry> Data { get; }

        public bool Equals(PathGeometryGroup other) => other != null && Enumerable.SequenceEqual(Data, other.Data);

        // Takes a group of possibly-animated paths and returns an animatable
        // of PathGeometryGroups. Returns true if it succeeds without issues.
        // Even if false is returned a best-effort animatable is returned.
        internal static bool TryGroupPaths(
            TranslationContext context,
            IEnumerable<Path> paths,
            out Animatable<PathGeometryGroup> result)
        {
            // Store the keyframes in a dictionary, keyed by frame.
            var ps = paths.ToArray();

            var groupsByFrame = new Dictionary<double, GeometryKeyFrame[]>(new FrameComparer(context))
            {
                { 0, new GeometryKeyFrame[ps.Length] },
            };

            for (var i = 0; i < ps.Length; i++)
            {
                var p = ps[i];

                // Add the initial value.
                groupsByFrame[0][i] = new GeometryKeyFrame(p, p.Data.InitialValue, HoldEasing.Instance);

                // Add any keyframes.
                foreach (var kf in p.Data.KeyFrames.ToArray().Skip(1))
                {
                    // See if there's a key frame at the frame number already.
                    if (!groupsByFrame.TryGetValue(kf.Frame, out var array))
                    {
                        array = new GeometryKeyFrame[ps.Length];
                        groupsByFrame.Add(kf.Frame, array);
                    }

                    array[i] = new GeometryKeyFrame(p, kf.Value, kf.Easing);
                }
            }

            // Make sure that every frame has a geometry from each path.
            // For any missing path, fill the hole with the path from the
            // previous frame.
            var frames = groupsByFrame.OrderBy(kvp => kvp.Key).Select(kvp => (frame: kvp.Key, geometries: kvp.Value)).ToArray();
            var previousGeometries = frames[0].geometries;
            var success = true;

            // Start from the second frame. The initial frame (0) will always have a value (.InitialValue).
            foreach (var (frame, geometries) in frames.Skip(1))
            {
                // Get the easing for this frame.
                var easings = geometries.Where(g => g != null).Select(g => g.Easing).Distinct().ToArray();
                if (easings.Length > 1)
                {
                    // There are conflicting easings. We can't currently handle that.
                    success = false;
                }

                for (var i = 0; i < geometries.Length; i++)
                {
                    if (geometries[i] == null)
                    {
                        // The frame doesn't have a correponding geometry for this path.
                        // Use the geometry from the previous frame, but with the easing
                        // from this frame.
                        geometries[i] = previousGeometries[i].CloneWithDifferentEasing(easings[0]);

                        // It's correct to use the previous frame's path if it isn't animated, but if
                        // it is animated it would need to be interpolated to be correct. We currently
                        // don't handle interpolation of paths in the translator, so indicate that we
                        // weren't able to do things correctly.
                        if (geometries[i].Path.Data.IsAnimated)
                        {
                            // This is a case that we can't handle correctly.
                            success = false;
                        }
                    }
                }

                previousGeometries = geometries;
            }

            // Every entry in frames now has path data. Return the groups.
            result =
                new Animatable<PathGeometryGroup>(
                    keyFrames:
                        (from f in frames
                         let firstGeometry = f.geometries[0]
                         let easing = firstGeometry.Easing
                         let geometryGroup = new PathGeometryGroup(f.geometries.Select(g => g.Geometry).ToArray(), easing)
                         select new KeyFrame<PathGeometryGroup>(f.frame, geometryGroup, easing)).ToArray(),
                    propertyIndex: null);

            return success;
        }

        sealed class GeometryKeyFrame
        {
            internal GeometryKeyFrame(Path path, PathGeometry geometry, Easing easing)
            {
                Path = path;
                Geometry = geometry;
                Easing = easing;
            }

            internal GeometryKeyFrame CloneWithDifferentEasing(Easing easing)
                => new GeometryKeyFrame(Path, Geometry, easing);

            internal Path Path { get; }

            internal Easing Easing { get; }

            internal PathGeometry Geometry { get; }
        }

        sealed class FrameComparer : IEqualityComparer<double>
        {
            readonly TranslationContext _context;

            internal FrameComparer(TranslationContext context)
            {
                _context = context;
            }

            public bool Equals(double x, double y) => ProgressOf(x) == ProgressOf(y);

            public int GetHashCode(double obj) => ProgressOf(obj).GetHashCode();

            // Converts a frame number into a progress value.
            float ProgressOf(double value) =>
                (float)((value - _context.StartTime) / _context.DurationInFrames);
        }
    }
}
