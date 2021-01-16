// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// The context in which a <see cref="RenderingContents.RenderingContent"/> is rendered.
    /// </summary>
    abstract class RenderingContext
    {
        public static RenderingContext Null { get; } = new NullRenderingContext();

        public abstract RenderingContext WithTimeOffset(double timeOffset);

        public abstract RenderingContext WithOffset(Vector3 offset);

        public virtual IReadOnlyList<RenderingContext> SubContexts => Array.Empty<RenderingContext>();

        public abstract bool IsAnimated { get; }

        public bool IsFlattened => SubContexts.All(context => context.IsFlattened);

        public static RenderingContext Compose(IEnumerable<RenderingContext> renderingContexts)
            => ComposeAlreadyFlattened(Flatten(renderingContexts).ToArray());

        public static RenderingContext Compose(params RenderingContext[] renderingContexts)
            => Compose((IList<RenderingContext>)renderingContexts);

        public static RenderingContext Compose(IList<RenderingContext> renderingContexts)
            => renderingContexts.Count switch
            {
                0 => Null,
                1 => renderingContexts[0],
                _ => ComposeAlreadyFlattened(Flatten(renderingContexts).ToArray()),
            };

        static RenderingContext ComposeAlreadyFlattened(IReadOnlyList<RenderingContext> renderingContexts)
            => renderingContexts.Count switch
            {
                0 => Null,
                1 => renderingContexts[0],
                _ => new CompositeRenderingContext(renderingContexts),
            };

        public static RenderingContext operator +(RenderingContext a, RenderingContext b)
            => Compose(a, b);

        public RenderingContext Where(Func<RenderingContext, bool> predicate)
        {
            if (this is CompositeRenderingContext composite)
            {
                return Compose(composite.SubContexts.Where(predicate));
            }
            else if (predicate(this))
            {
                return this;
            }
            else
            {
                return Null;
            }
        }

        // Move all of the contexts of the given type to the start.
        public RenderingContext MoveToTop<T>()
            where T : RenderingContext
        {
            if (SubContexts.Count == 0)
            {
                return this;
            }

            var accumulatorOfT = new List<T>(SubContexts.Count / 2);
            var accumulatorOfNotT = new List<RenderingContext>(SubContexts.Count);

            foreach (var context in SubContexts)
            {
                if (context is T asT)
                {
                    accumulatorOfT.Add(asT);
                }
                else
                {
                    accumulatorOfNotT.Add(context);
                }
            }

            return Compose(accumulatorOfT) + Compose(accumulatorOfNotT);
        }

        /// <summary>
        /// Returns a context with all items except any of <typeparamref name="T"/> that
        /// return false for the predicate.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="RenderingContext"/> to filter.</typeparam>
        /// <returns>A filtered <see cref="RenderingContext"/>.</returns>
        public RenderingContext Filter<T>(Func<T, bool> predicate)
            where T : RenderingContext
            => Where(item => item is T itemAsT ? predicate(itemAsT) : true);

        public static IEnumerable<RenderingContext> Filter<T>(IEnumerable<RenderingContext> items, Func<T, bool> predicate)
            => items.Where(item => !(item is T itemT) || predicate(itemT));

        public RenderingContext GetFlattened()
            => IsFlattened
                ? this
                : Compose(Flatten(SubContexts));

        static IEnumerable<RenderingContext> Flatten(IEnumerable<RenderingContext> input)
        {
            foreach (var item in input)
            {
                switch (item)
                {
                    case NullRenderingContext nullContext:
                        break;
                    case CompositeRenderingContext compositeContext:
                        foreach (var subContext in compositeContext.SubContexts)
                        {
                            yield return subContext;
                        }

                        break;
                    default:
                        yield return item;
                        break;
                }
            }
        }

        sealed class CompositeRenderingContext : RenderingContext
        {
            readonly IReadOnlyList<RenderingContext> _subContexts;

            internal CompositeRenderingContext(IReadOnlyList<RenderingContext> subContexts)
            {
                _subContexts = subContexts;
            }

            CompositeRenderingContext(IEnumerable<RenderingContext> subContexts)
                : this(subContexts.ToArray())
            {
            }

            public override IReadOnlyList<RenderingContext> SubContexts => _subContexts;

            public override bool IsAnimated => SubContexts.Any(item => item.IsAnimated);

            public override sealed RenderingContext WithOffset(Vector3 offset)
                => new CompositeRenderingContext(SubContexts.Select(item => item.WithOffset(offset)));

            public override RenderingContext WithTimeOffset(double timeOffset)
                 => IsAnimated
                    ? new CompositeRenderingContext(SubContexts.Select(item => item.WithTimeOffset(timeOffset)))
                    : this;

            public override string ToString()
                => $"{(IsAnimated ? "Animated" : "Static")} RenderingContext[{SubContexts.Count}]";
        }

        sealed class NullRenderingContext : RenderingContext
        {
            public override bool IsAnimated => false;

            public override sealed RenderingContext WithOffset(Vector3 offset) => this;

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() => "Null";
        }
    }
}
