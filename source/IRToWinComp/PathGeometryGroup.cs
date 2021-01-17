// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IRToWinComp
{
    /// <summary>
    /// A group of <see cref="PathGeometry"/>.
    /// </summary>
    sealed class PathGeometryGroup : IEquatable<PathGeometryGroup>
    {
        PathGeometryGroup(PathGeometry[] data)
        {
            Data = data;
        }

        /// <summary>
        /// The geometries in the group.
        /// </summary>
        public IReadOnlyList<PathGeometry> Data { get; }

        public bool Equals(PathGeometryGroup? other) => other != null && Enumerable.SequenceEqual(Data, other.Data);

        /// <summary>
        /// Takes a group of possibly-animated paths and returns an animatable
        /// of PathGeometryGroups. <paramref name="groupingIsPerfect"/> is true
        /// if there were no issues in the grouping.
        /// Even if <paramref name="groupingIsPerfect"/> is false, a best-effort
        /// animatable is returned that in a lot of cases will look ok.</summary>
        /// <returns>The paths, grouped.</returns>
        internal static Animatable<PathGeometryGroup> GroupPaths(
            ShapeLayerContext context,
            IReadOnlyList<Path> paths,
            out bool groupingIsPerfect)
        {
            // Ideally each of the paths would have identical key frames with identical frame numbers.
            // For example:
            //  paths[0] has key frames for frame 2, 5, 7, 15.
            //  paths[1] has key frames for frame 2, 5, 7, 15.
            //  paths[2] has key frames for frame 2, 5, 7, 15.
            //
            // However in practice the key frames for each path could be different, e.g.:
            //  paths[0] has key frames for frame 2, 5, 7, 15.
            //  paths[1] has key frames for frame 3, 5, 7, 17.
            //  paths[2] has key frames for frame 1, 2, 3, 4, 5, 6.
            //
            // We can handle the ideal case perfectly as long as the easings are the same for
            // each path. We'll do a compromise in other cases that may not look quite right but
            // is preferable to just failing.
            //
            //
            // Algorithm:
            // ==========
            //
            // For each key frame number, create a (frame, easing, listOfPathGeometry) triple.
            // For the ideal example:
            // (2,  easing, paths[0].Data.KeyFrames[0], paths[1].Data.KeyFrames[0], paths[2].Data.KeyFrames[0]),
            // (5,  easing, paths[0].Data.KeyFrames[1], paths[1].Data.KeyFrames[1], paths[2].Data.KeyFrames[1]),
            // (7,  easing, paths[0].Data.KeyFrames[2], paths[1].Data.KeyFrames[2], paths[2].Data.KeyFrames[2]),
            // (15, easing, paths[0].Data.KeyFrames[3], paths[1].Data.KeyFrames[3], paths[2].Data.KeyFrames[3])
            //
            // For the non-ideal example:
            // (1,  easing,                       null,                       null, paths[2].Data.KeyFrames[0]),
            // (2,  easing, paths[0].Data.KeyFrames[0],                       null, paths[2].Data.KeyFrames[1]),
            // (3,  easing,                       null, paths[1].Data.KeyFrames[0], paths[2].Data.KeyFrames[2]),
            // (4,  easing,                       null,                       null, paths[2].Data.KeyFrames[3]),
            // (5,  easing, paths[0].Data.KeyFrames[1], paths[1].Data.KeyFrames[1], paths[2].Data.KeyFrames[4]),
            // (6,  easing,                       null,                       null, paths[2].Data.KeyFrames[5]),
            // (7,  easing, paths[0].Data.KeyFrames[2], paths[1].Data.KeyFrames[2],                       null),
            // (15, easing, paths[0].Data.KeyFrames[3],                       null,                       null),
            // (17, easing,                       null, paths[1].Data.KeyFrames[3],                       null)
            //
            // Then fill in the null entries with values that are interpolated from the surrounding data.
            // NOTE: we currently don't try very hard to interpolate the values, so the results in the
            // non-ideal cases still have room for improvement. Luckily the ideal case is quite common.
            //

            // Test for the simplest case - all the paths are non animated.
            if (paths.All(p => !p.Data.IsAnimated))
            {
                var group = new PathGeometryGroup(paths.Select(p => p.Data.InitialValue).ToArray());
                groupingIsPerfect = true;
                return new Animatable<PathGeometryGroup>(group);
            }

            // At least some of the paths are animated. Create the data structure.
            var records = CreateGroupRecords(paths.Select(p => p.Data).ToArray()).ToArray();

            // We are succeeding if the easing is correct in each record. If not we'll
            // indicate that the result is less than perfect.
            groupingIsPerfect = records.Select(g => g.EasingIsCorrect).Min();

            // Fill in the nulls in the data structure. Ideally we'd fill these in with interpolated
            // values, but interpolation is difficult, so for now we just copy the nearest value.
            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                for (var frameIndex = 0; frameIndex < records.Length; frameIndex++)
                {
                    // Get the key frame for the current frame number.
                    var keyFrame = records[frameIndex].Geometries[pathIndex];
                    if (keyFrame is null)
                    {
                        // There is no key frame for the current frame number.
                        // Interpolate a key frame to replace the null.
                        records[frameIndex].Geometries[pathIndex] =
                            InterpolateKeyFrame(records, pathIndex, frameIndex);

                        // Indicate that the grouping is not perfect due to having
                        // to interpolate the path value for this keyFrame.
                        groupingIsPerfect = false;
                    }
                }
            }

            // Create the result by creating a key frame containing a PathGeometryGroup for each record,
            // and use the "preferred easing" to ease between the key frames.
            return
                new Animatable<PathGeometryGroup>(
                    keyFrames:
                        (from g in records
                         let geometryGroup = new PathGeometryGroup(g.Geometries.Select(h => h!.Value).ToArray())
                         select new KeyFrame<PathGeometryGroup>(g.Frame, geometryGroup, g.PreferredEasing)).ToArray());
        }

        static KeyFrame<PathGeometry> InterpolateKeyFrame(
            PathGeometryGroupKeyFrames[] records,
            int pathIndex,
            int frameIndex)
        {
            // We currently don't do proper interpolation - we just
            // find the non-null value that is closest to the current
            // frame number.
            //
            // Get the previous value and its frame number, and the
            // next value and its frame number, and choose the one
            // with the nearest frame number.
            var currentFrameNumber = records[frameIndex].Frame;
            KeyFrame<PathGeometry>? previousKeyFrame = null;
            KeyFrame<PathGeometry>? nextKeyFrame = null;
            if (frameIndex > 0)
            {
                // If there was a previous key frame, get it. It is guaranteed
                // by the algorithm that all records[<frameIndex] have non-null values.
                previousKeyFrame = records[frameIndex - 1].Geometries[pathIndex];
            }

            for (var nextFrameIndex = frameIndex; nextFrameIndex < records.Length && nextKeyFrame is null; nextFrameIndex++)
            {
                nextKeyFrame = records[nextFrameIndex].Geometries[pathIndex];
            }

            // Choose the frame that is closest.
            KeyFrame<PathGeometry> closestKeyFrame;
            if (previousKeyFrame is null)
            {
                closestKeyFrame = nextKeyFrame!;
            }
            else if (nextKeyFrame is null)
            {
                closestKeyFrame = previousKeyFrame!;
            }
            else if (currentFrameNumber - previousKeyFrame.Frame < nextKeyFrame.Frame - currentFrameNumber)
            {
                closestKeyFrame = previousKeyFrame!;
            }
            else
            {
                closestKeyFrame = nextKeyFrame!;
            }

            // Return the value from the closest key frame.
            return
                new KeyFrame<PathGeometry>(currentFrameNumber, closestKeyFrame.Value, HoldEasing.Instance);
        }

        /// <summary>
        /// From a list of animatable paths, produce a record for each distinct key frame number.
        /// Each record consists of the frame number, a list of geometries for that frame number
        /// and the easing from the previous frame number. The list of geometries has an entry
        /// corresponding to each path, however the entry will be null if there is no geometry
        /// for that particular key frame.
        /// </summary>
        static IEnumerable<PathGeometryGroupKeyFrames> CreateGroupRecords(
            IReadOnlyList<Animatable<PathGeometry>> pathData)
        {
            // Get enumerators for the key frames in each of the Animatable<PathGeometry>s.
            var enumerators = pathData.Select(EnumerateKeyFrames).ToArray();

            // Start enumerating.
            foreach (var enumerator in enumerators)
            {
                enumerator.MoveNext();
            }

            // Get the current value from each enumerator.
            var curs = enumerators.Select(en => en.Current).ToArray();

            // Keep looping until all of the enumerators have been consumed.
            while (curs.Any(cur => !cur.isCompleted))
            {
                // Get the lowest frame number from the enumerators that have not yet been consumed.
                var currentFrame = curs.Where(cur => !cur.isCompleted).Select(cur => cur.keyFrame.Frame).Min();

                // Yield a group for all of the key frames that have a value for this frame number.
                // The frame number is NaN if the value is non-animated. Non-animated values are
                // always output.
                Easing? preferredEasing = null;
                var easingIsCorrect = true;
                var geometries = new KeyFrame<PathGeometry>?[pathData.Count];

                for (var i = 0; i < enumerators.Length; i++)
                {
                    var current = enumerators[i].Current;
                    var frameNumber = current.keyFrame.Frame;
                    if (double.IsNaN(frameNumber))
                    {
                        // The value is non-animated.
                        geometries[i] = current.keyFrame;
                    }
                    else if (frameNumber == currentFrame)
                    {
                        // The value has a key frame at the current frame number.
                        geometries[i] = current.keyFrame;
                        if (preferredEasing is null)
                        {
                            // Use the first easing we come across. This is arbitrary -
                            // we need an easing, but the various paths may have different easings
                            // The first one we find is correct for that particular path, and it
                            // might be correct for the other paths. The only way to get this
                            // completely correct would be to write an interpolator.
                            preferredEasing = current.keyFrame.Easing;
                        }
                        else if (preferredEasing != current.keyFrame.Easing)
                        {
                            easingIsCorrect = false;
                        }
                    }
                }

                // There should always be at least one keyframe for each frame number, so we will
                // always end up with a non-null preferred easing here.
                Debug.Assert(preferredEasing != null, "Invariant");

                yield return new PathGeometryGroupKeyFrames(currentFrame, geometries, preferredEasing!, easingIsCorrect);

                // Advance the enumerators that are on the current frame unless they are completed.
                // NOTE: we don't need to care about whether the enumerators are completed because
                //       they will continue to return the last value, but we might as well avoid
                //       some unnecessary work.
                for (var i = 0; i < enumerators.Length; i++)
                {
                    var enumerator = enumerators[i];
                    if (!enumerator.Current.isCompleted &&
                        enumerator.Current.keyFrame.Frame == currentFrame)
                    {
                        enumerator.MoveNext();
                        curs[i] = enumerator.Current;
                    }
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that yields each value from its key frames in sequence, or if the animatable
        /// is not animated, yields its initial value forever. The enumeration continues forever, repeatedly
        /// returning the final value after all of the other values have been returned, but with the
        /// isCompleted variable is set to indicate that all of the values have been returned. This is done
        /// to make the code simpler for the consumer so that it doesn't need to handle exceptions from
        /// calling .Current after getting to the end of the enumeration.
        /// </summary>
        static IEnumerator<(KeyFrame<PathGeometry> keyFrame, bool isCompleted)> EnumerateKeyFrames(
            Animatable<PathGeometry> animatable)
        {
            KeyFrame<PathGeometry> finalKeyFrame;

            if (!animatable.IsAnimated)
            {
                finalKeyFrame = new KeyFrame<PathGeometry>(double.NaN, animatable.InitialValue, HoldEasing.Instance);
            }
            else
            {
                // Yield each value.
                var keyFrames = animatable.KeyFrames;
                finalKeyFrame = keyFrames[keyFrames.Count - 1];
                foreach (var keyFrame in keyFrames)
                {
                    yield return (keyFrame, isCompleted: false);
                }
            }

            // Keep yielding the final key frame.
            while (true)
            {
                yield return (finalKeyFrame, isCompleted: true);
            }
        }

        /// <summary>
        /// The grouped key frames for a particular frame number of an animated path geometry group.
        /// </summary>
        sealed class PathGeometryGroupKeyFrames
        {
            internal PathGeometryGroupKeyFrames(
                double frame,
                KeyFrame<PathGeometry>?[] geometries,
                Easing preferredEasing,
                bool easingIsCorrect)
            {
                Frame = frame;
                Geometries = geometries;
                PreferredEasing = preferredEasing;
                EasingIsCorrect = easingIsCorrect;
            }

            internal double Frame { get; }

            internal KeyFrame<PathGeometry>?[] Geometries { get; }

            internal Easing PreferredEasing { get; }

            internal bool EasingIsCorrect { get; }
        }
    }
}
