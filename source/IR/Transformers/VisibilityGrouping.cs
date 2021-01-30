// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;
using Microsoft.VisualBasic;
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
                Console.WriteLine(string.Join(", ", layer.Select(r => r.Id).OrderBy(i => i)));
            }
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

        sealed class RenderingWithVisibility
        {
            static int s_counter;
            readonly List<VisibilitySegmentWithRendering> _visbilitySegments = new List<VisibilitySegmentWithRendering>();

            internal RenderingWithVisibility(Rendering rendering)
            {
                Id = s_counter++;

                Rendering = rendering;

                // Calculate the visiblity of the rendering by evaluating
                // all of its visiblity contexts and combining into a single
                // composite visibility.
                Visibility =
                    VisibilityRenderingContext.Combine(
                        rendering.Context.OfType<VisibilityRenderingContext>());
            }

            public int Id { get; }

            public Rendering Rendering { get; }

            public VisibilityRenderingContext Visibility { get; }

            public IReadOnlyList<VisibilitySegmentWithRendering> VisibilitySegments => _visbilitySegments;

            public void AddVisibilitySegment(VisibilitySegmentWithRendering segment)
            {
                _visbilitySegments.Add(segment);
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

                if (!(Rendering.Content is PathRenderingContent myPath))
                {
                    // For now we only support paths.
                    throw TODO;
                }

                // Remove any that have incompatible content.
                var rejects = new List<RenderingWithVisibility>();
                foreach (var candidate in Orthogonal)
                {
                    var candidateContent = candidate.Rendering.Content;

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
