// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;
using static Microsoft.Toolkit.Uwp.UI.Lottie.IR.Exceptions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.Transformers
{
    sealed class VisibilityGrouping
    {
        internal static void CreateVisibilityGroups(IEnumerable<Rendering> input)
        {
            var renderingsWithVisibility =
                input.Select(r => new RenderingWithVisibility(r)).
                OrderBy(r => r.Visibility.StateChangeTimes.First()).
                ToArray();

            // Calculate the distinct time segments for all of the renderings.
            // For example, if one Rendering is visible from 0 to 20, and another
            // Rendering is visible from 8 to 12, there are 3 distinct segments:
            //   0 to 8, 8 to 12, and 12 to 20.
            var vswr = GetVisibilitySegmentWithRenderings(renderingsWithVisibility);

            // Set the Above and Below sets for each rendering. These deterine the order
            // in which renderings must be layered.
            foreach (var visiblity in vswr)
            {
                for (var i = 0; i < visiblity.Renderings.Count - 1; i++)
                {
                    var rCurrent = visiblity.Renderings[i];
                    var rAbove = visiblity.Renderings[i + 1];
                    rCurrent.Above.Add(rAbove);
                    rAbove.Below.Add(rCurrent);
                }
            }

            // Calculate the set of orthogonal renderings for each rendering.
            // An orthogonal rendering is one that has no drawing order
            // relationship.
            foreach (var rendering in renderingsWithVisibility)
            {
                rendering.CalculateOrthogonalSet(renderingsWithVisibility);
            }

            var layers = GroupRenderings(renderingsWithVisibility).ToArray();
            foreach (var layer in layers)
            {
                Smoosh(layer);
                Console.WriteLine(string.Join(", ", layer.Select(r => r.Id).OrderBy(i => i)));
            }
        }

        static void Smoosh(IReadOnlyList<RenderingWithVisibility> layer)
        {
            var smooshedContent = layer.Select(rwv => (rwv.Content, rwv.Visibility)).Aggregate(SmooshContent);
            var smooshedContext = layer.Select(rwv => (rwv.NormalFormContexts, rwv.Visibility)).Aggregate(SmooshContext);
        }

        static (IReadOnlyList<NormalFormContext>, VisibilityRenderingContext visibility) SmooshContext(
            (IReadOnlyList<NormalFormContext> context, VisibilityRenderingContext visibility) a,
            (IReadOnlyList<NormalFormContext> context, VisibilityRenderingContext visibility) b)
        {
            throw TODO;
        }

        static (RenderingContent content, VisibilityRenderingContext visibility) SmooshContent(
            (RenderingContent content, VisibilityRenderingContext visibility) a,
            (RenderingContent content, VisibilityRenderingContext visibility) b)
        {
            // Create a new combined visibility context.
            var smooshedVisibility = VisibilityRenderingContext.CombineOr(new[] { a.visibility, b.visibility });

            // Create a new combined content.
            switch (a.content)
            {
                case PathRenderingContent pathRenderingContent:
                    if (b.content is not PathRenderingContent)
                    {
                        throw TODO;
                    }

                    return (content: SmooshPathRenderingContent(
                                        (pathRenderingContent, a.visibility),
                                        ((PathRenderingContent)b.content, b.visibility)),
                        visibility: smooshedVisibility);

                default:
                    throw TODO;
            }
        }

        static PathRenderingContent SmooshPathRenderingContent(
            (PathRenderingContent content, VisibilityRenderingContext visibilty) a,
            (PathRenderingContent content, VisibilityRenderingContext visibilty) b)
        {
            if (a.content.BezierSegmentCount != b.content.BezierSegmentCount ||
                a.content.IsClosed != b.content.IsClosed)
            {
                // Grouping should not have been allowed if the paths
                // are not compatible.
                throw Unreachable;
            }

            if (a.content.IsAnimated)
            {
                var animatedA = (PathRenderingContent.Animated)a.content;
                if (b.content.IsAnimated)
                {
                    return SmooshPathRenderingContent((animatedA, a.visibilty), ((PathRenderingContent.Animated)b.content, b.visibilty));
                }
                else
                {
                    return SmooshPathRenderingContent((animatedA, a.visibilty), ((PathRenderingContent.Static)b.content, b.visibilty));
                }
            }
            else
            {
                var staticA = (PathRenderingContent.Static)a.content;
                if (b.content.IsAnimated)
                {
                    return SmooshPathRenderingContent((staticA, a.visibilty), ((PathRenderingContent.Animated)b.content, b.visibilty));
                }
                else
                {
                    return SmooshPathRenderingContent((staticA, a.visibilty), ((PathRenderingContent.Static)b.content, b.visibilty));
                }
            }
        }

        static PathRenderingContent SmooshPathRenderingContent(
            (PathRenderingContent.Static content, VisibilityRenderingContext visibilty) a,
            (PathRenderingContent.Static content, VisibilityRenderingContext visibilty) b)
        {
            // If both contents are the same, just return one.
            if (a.content.Geometry.BezierSegments.Equals(b.content.Geometry.BezierSegments))
            {
                return a.content;
            }

            // The contents are different. Convert to animated.
            var keyFrames = new KeyFrame<PathGeometry>[] {
                                    new KeyFrame<PathGeometry>(0, a.content.Geometry, HoldEasing.Instance),
                                    new KeyFrame<PathGeometry>(b.visibilty.GetVisibleSegments().First().Offset, b.content.Geometry, HoldEasing.Instance),
                                    };
            var animatedGeometry = new Animatable<PathGeometry>(keyFrames);
            return new PathRenderingContent.Animated(animatedGeometry);
        }

        static PathRenderingContent SmooshPathRenderingContent(
            (PathRenderingContent.Animated content, VisibilityRenderingContext visibilty) a,
            (PathRenderingContent.Animated content, VisibilityRenderingContext visibilty) b)
        {
            throw TODO;
        }

        static PathRenderingContent SmooshPathRenderingContent(
            (PathRenderingContent.Static content, VisibilityRenderingContext visibilty) a,
            (PathRenderingContent.Animated content, VisibilityRenderingContext visibilty) b)
        {
            throw TODO;
        }

        static PathRenderingContent SmooshPathRenderingContent(
            (PathRenderingContent.Animated content, VisibilityRenderingContext visibilty) a,
            (PathRenderingContent.Static content, VisibilityRenderingContext visibilty) b)
        {
            // If the new static content is the same as the last key frame in the animated content
            // just return the animated content.
            var lastKeyFrame = a.content.Geometry.KeyFrames.Last();
            if (lastKeyFrame.Value.Equals(b.content.Geometry))
            {
                return a.content;
            }

            var keyFrames = a.content.Geometry.KeyFrames.Append(
                                new KeyFrame<PathGeometry>(b.visibilty.GetVisibleSegments().First().Offset, b.content.Geometry, HoldEasing.Instance));

            var animatedGeometry = new Animatable<PathGeometry>(keyFrames);
            return new PathRenderingContent.Animated(animatedGeometry);
        }

        static IEnumerable<VisibilitySegmentWithRendering> GetVisibilitySegmentWithRenderings(IReadOnlyList<RenderingWithVisibility> renderings)
        {
            var timeSegments =
                VisibilityRenderingContext.GetVisibilitySegments(
                    renderings.Select(r => r.Visibility)).ToArray();

            // For each time segment, get the renderings that are contained in it.
            foreach (var ts in timeSegments)
            {
                var result = new VisibilitySegmentWithRendering(ts, renderings.Where(r => r.Visibility.IsVisibleDuring(ts)).ToArray());

                foreach (var r in result.Renderings)
                {
                    // Associate the time segment with the rendering.
                    r.AddVisibilitySegment(result);
                }

                yield return result;
            }
        }

        static IEnumerable<IReadOnlyList<RenderingWithVisibility>> GroupRenderings(IEnumerable<RenderingWithVisibility> renderings)
        {
            var remaining = renderings.ToHashSet();
            foreach (var r in remaining)
            {
                r.InitializeBelowScratch();
            }

            while (remaining.Count > 1)
            {
                // Find the bottom-most renderings. These are the
                // renderings that having no renderings below them.
                // Order by the number of orthogonal renderings so
                // the first rendering is one tha that has the most
                // orthogonals.
                var bottomMost = remaining.
                                    Where(r => r.BelowScratch.Count == 0).
                                    OrderByDescending(r => r.Orthogonal.Count).
                                    ToArray();

                var mostBottomMost = bottomMost[0];

                // Accumulate the list of renderings, starting with the most
                // bottom-most, and adding any other bottom-mosts that are orthogonal.
                var result = new List<RenderingWithVisibility>();
                result.Add(mostBottomMost);

                foreach (var r in bottomMost.Skip(1))
                {
                    // Any of the other bottom-most that are orthogonal can
                    // be included.
                    if (r.Orthogonal.Contains(mostBottomMost))
                    {
                        result.Add(r);
                    }
                }

                // Remove all of the renderings we are about to return.
                remaining.ExceptWith(result);

                // Remove all references to the items in bottomMost from the remaining.
                // This will ensure that the items being returned will not be considered
                // for grouping in subsequent iterations.
                foreach (var r in remaining)
                {
                    r.BelowScratch.ExceptWith(result);
                    r.Orthogonal.ExceptWith(result);
                }

                yield return result.OrderBy(r => r.Visibility.StateChangeTimes.First()).ToArray();
            }

            if (remaining.Count > 0)
            {
                yield return remaining.ToArray();
            }
        }

        sealed class VisibilitySegmentWithRendering
        {
            internal VisibilitySegmentWithRendering(
                in TimeSegment segment,
                IReadOnlyList<RenderingWithVisibility> renderings)
            {
                Segment = segment;
                Renderings = renderings;
            }

            public TimeSegment Segment { get; }

            public IReadOnlyList<RenderingWithVisibility> Renderings { get; }
        }

        /// <summary>
        /// Extracts a <see cref="VisibilityRenderingContext"/>s from a <see cref="Rendering"/>
        /// and provides helpers for determining orthogonal visibility layers.
        /// </summary>
        sealed class RenderingWithVisibility
        {
            static int s_counter;
            readonly List<VisibilitySegmentWithRendering> _visibilitySegments =
                new List<VisibilitySegmentWithRendering>();

            internal RenderingWithVisibility(Rendering rendering)
            {
                Id = s_counter++;

                // Calculate the visiblity of the rendering by evaluating
                // all of its visiblity contexts and combining into a single
                // composite visibility.
                Visibility =
                    VisibilityRenderingContext.CombineAnd(
                        rendering.Context.OfType<VisibilityRenderingContext>());

                Content = rendering.Content;

                // Convert the context to normal form, but without the visibility contexts.
                NormalFormContexts =
                    NormalFormContext.ToNormalForm(
                        rendering.Context.Without<VisibilityRenderingContext>(),
                        Visibility.GetVisibleSegments()).ToArray();
            }

            public int Id { get; }

            public RenderingContent Content { get; }

            public IReadOnlyList<NormalFormContext> NormalFormContexts { get; }

            public VisibilityRenderingContext Visibility { get; }

            public IReadOnlyList<VisibilitySegmentWithRendering> VisibilitySegments => _visibilitySegments;

            public void AddVisibilitySegment(VisibilitySegmentWithRendering segment)
            {
                _visibilitySegments.Add(segment);
            }

            internal HashSet<RenderingWithVisibility> Above { get; } = new HashSet<RenderingWithVisibility>();

            internal HashSet<RenderingWithVisibility> Below { get; } = new HashSet<RenderingWithVisibility>();

            internal HashSet<RenderingWithVisibility> BelowScratch { get; private set; } = new HashSet<RenderingWithVisibility>();

            internal void InitializeBelowScratch()
            {
                BelowScratch = new HashSet<RenderingWithVisibility>(Below);
            }

            internal HashSet<RenderingWithVisibility> Orthogonal { get; private set; } = new HashSet<RenderingWithVisibility>();

            internal int Z => Below.Count == 0 ? 0 : Below.Max(r => r.Z) + 1;

            internal void CalculateOrthogonalSet(IEnumerable<RenderingWithVisibility> allRenderings)
            {
                // Start with all renderings. Remove any that do not have compatible
                // content, or that have an rendering order relationship with this rendering.
                Orthogonal = new HashSet<RenderingWithVisibility>(allRenderings);
                Orthogonal.Remove(this);

                if (!(Content is PathRenderingContent myPath))
                {
                    // For now we only support paths.
                    throw TODO;
                }

                // Remove any that have incompatible content.
                var rejects = new List<RenderingWithVisibility>();
                foreach (var candidate in Orthogonal)
                {
                    var candidateContent = candidate.Content;

                    // For now we only support paths.
                    if (candidateContent is PathRenderingContent candidatePath)
                    {
                        if (candidatePath.BezierSegmentCount != myPath.BezierSegmentCount)
                        {
                            rejects.Add(candidate);
                        }
                    }
                    else
                    {
                        rejects.Add(candidate);
                    }
                }

                Orthogonal.ExceptWith(rejects);

                // Remove any that have an ordering relationship with this rendering.
                Orthogonal.ExceptWith(GetAll(r => r.Above));
                Orthogonal.ExceptWith(GetAll(r => r.Below));
            }

            IEnumerable<RenderingWithVisibility> GetAll(Func<RenderingWithVisibility, IEnumerable<RenderingWithVisibility>> collection)
            {
                foreach (var r in collection(this))
                {
                    yield return r;
                    foreach (var subR in r.GetAll(collection))
                    {
                        yield return subR;
                    }
                }
            }

            public override string ToString() => $"#{Id} Z{Z}  {Visibility}";
        }
    }
}
