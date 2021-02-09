// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// Describes a set of <see cref="RenderingContext"/>s that are in
    /// "normal form", i.e. they have a well-known order, and any contexts
    /// that do not contribute to the result are null.
    /// </summary>
    /// <remarks>The well-known order is:
    /// <see cref="Visibility"/>, <see cref="Metadata"/>, <see cref="Size"/>,
    /// <see cref="Position"/>, <see cref="Opacity"/>,= <see cref="Fill"/>.
    /// </remarks>
    sealed class NormalFormContext
    {
        NormalFormContext(IReadOnlyList<TimeSegment> visibility)
        {
            Visibility = visibility;
        }

        /// <summary>
        /// When the context is visible.
        /// </summary>
        public IReadOnlyList<TimeSegment> Visibility { get; }

        public MetadataRenderingContext? Metadata { get; private set; }

        public SizeRenderingContext? Size { get; private set; }

        public PositionRenderingContext? Position { get; private set; }

        public OpacityRenderingContext? Opacity { get; private set; }

        public FillRenderingContext? Fill { get; private set; }

        /// <summary>
        /// Combines 2 <see cref="NormalFormContext"/>s.
        /// </summary>
        /// <returns>The combined <see cref="NormalFormContext"/>.</returns>
        public static NormalFormContext Combine(NormalFormContext a, NormalFormContext b)
        {
            // TODO - assert that the visibilities are orthogonal.
            // TODO - animate each of the values.
            return new NormalFormContext(Array.Empty<TimeSegment>());
        }

        // If a context is added for which the slot is already filled,
        // or if there is something in another slot that would be evaluated
        // later and depend on the new context, then start a new context.
        public static IEnumerable<NormalFormContext> ToNormalForm(
            IEnumerable<RenderingContext> contexts,
            IEnumerable<TimeSegment> visibility)
        {
            var visibilityList = visibility.ToArray();

            NormalFormContext? current = null;

            // Gets the current NormalFormContext, creating it
            // if it's null.
            NormalFormContext GetCurrent() => current ??= new NormalFormContext(visibilityList);

            foreach (var context in contexts)
            {
                switch (context)
                {
                    case MetadataRenderingContext metadata:
                        if (current is not null &&
                            (current.Metadata is not null ||
                             metadata.IsOrderDependentWith(current.Size) ||
                             metadata.IsOrderDependentWith(current.Position) ||
                             metadata.IsOrderDependentWith(current.Opacity) ||
                             metadata.IsOrderDependentWith(current.Fill)))
                        {
                            yield return current;
                            current = null;
                        }

                        GetCurrent().Metadata = metadata;
                        break;

                    case SizeRenderingContext size:
                        if (current is not null &&
                            (current.Size is not null ||
                             size.IsOrderDependentWith(current.Position) ||
                             size.IsOrderDependentWith(current.Opacity) ||
                             size.IsOrderDependentWith(current.Fill)))
                        {
                            yield return current;
                            current = null;
                        }

                        GetCurrent().Size = size;
                        break;

                    case PositionRenderingContext position:
                        if (current is not null &&
                            (current.Position is not null ||
                             position.IsOrderDependentWith(current.Opacity) ||
                             position.IsOrderDependentWith(current.Fill)))
                        {
                            yield return current;
                            current = null;
                        }

                        GetCurrent().Position = position;
                        break;

                    case OpacityRenderingContext opacity:
                        if (current is not null &&
                            (current.Opacity is not null ||
                             opacity.IsOrderDependentWith(current.Fill)))
                        {
                            yield return current;
                            current = null;
                        }

                        GetCurrent().Opacity = opacity;
                        break;

                    case FillRenderingContext fill:
                        if (current is not null && current.Fill is not null)
                        {
                            yield return current;
                            current = null;
                        }

                        GetCurrent().Fill = fill;
                        break;

                    default:
                        throw Exceptions.Unreachable;
                }
            }

            if (current is not null)
            {
                yield return current;
            }
        }
    }
}
