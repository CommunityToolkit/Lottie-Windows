﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts
{
    /// <summary>
    /// The context in which a <see cref="RenderingContents.RenderingContent"/> is rendered.
    /// </summary>
    abstract class RenderingContext : IEnumerable<RenderingContext>
    {
        readonly IReadOnlyList<RenderingContext> _subContexts = Array.Empty<RenderingContext>();

        RenderingContext(IReadOnlyList<RenderingContext> subContexts)
        {
            _subContexts = subContexts;
        }

        private protected RenderingContext()
        {
        }

        public static RenderingContext Null { get; } = new NullRenderingContext();

        public abstract RenderingContext WithTimeOffset(double timeOffset);

        public abstract RenderingContext WithOffset(Vector2 offset);

        public int SubContextCount => _subContexts.Count;

        public abstract bool IsAnimated { get; }

        public bool IsFlattened => _subContexts.All(context => context.IsFlattened);

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

        /// <summary>
        /// Moves all of the contexts of the given type to the start.
        /// </summary>
        /// <typeparam name="T">The type of context that will moved.</typeparam>
        /// <returns>
        /// A context with the same sub-contexts, but with
        /// any context of type <typeparamref name="T"/> moved to the start.
        /// </returns>
        public RenderingContext MoveToStart<T>()
            where T : RenderingContext
        {
            var (t, notT) = Partition<T>();
            return t + notT;
        }

        /// <summary>
        /// Moves all of the contexts of the given type to the end.
        /// </summary>
        /// <typeparam name="T">The type of context that will moved.</typeparam>
        /// <returns>
        /// A context with the same sub-contexts, but with
        /// any context of type <typeparamref name="T"/> moved to the end.
        /// </returns>
        public RenderingContext MoveToEnd<T>()
                where T : RenderingContext
        {
            var (t, notT) = Partition<T>();
            return notT + t;
        }

        (RenderingContext t, RenderingContext notT) Partition<T>()
            where T : RenderingContext
        {
            if (_subContexts.Count == 0)
            {
                return (Null, this);
            }

            var accumulatorOfT = new List<T>(_subContexts.Count / 2);
            var accumulatorOfNotT = new List<RenderingContext>(_subContexts.Count);

            foreach (var context in _subContexts)
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

            return (Compose(accumulatorOfT), Compose(accumulatorOfNotT));
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
                : Compose(Flatten(_subContexts));

        static IEnumerable<RenderingContext> Flatten(IEnumerable<RenderingContext> input)
        {
            foreach (var item in input)
            {
                switch (item)
                {
                    case NullRenderingContext _:
                        break;
                    case CompositeRenderingContext compositeContext:
                        foreach (var subContext in compositeContext)
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

        IEnumerator<RenderingContext> IEnumerable<RenderingContext>.GetEnumerator()
            => _subContexts.Count == 0
                ? ((IEnumerable<RenderingContext>)new RenderingContext[] { this }).GetEnumerator()
                : _subContexts.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<RenderingContext>)this).GetEnumerator();

        sealed class CompositeRenderingContext : RenderingContext
        {
            internal CompositeRenderingContext(IReadOnlyList<RenderingContext> subContexts)
                : base(subContexts)
            {
            }

            CompositeRenderingContext(IEnumerable<RenderingContext> subContexts)
                : this(subContexts.ToArray())
            {
            }

            public IReadOnlyList<RenderingContext> SubContexts => _subContexts;

            public override bool IsAnimated => _subContexts.Any(item => item.IsAnimated);

            public override sealed RenderingContext WithOffset(Vector2 offset)
                => new CompositeRenderingContext(_subContexts.Select(item => item.WithOffset(offset)));

            public override RenderingContext WithTimeOffset(double timeOffset)
                 => IsAnimated
                    ? new CompositeRenderingContext(_subContexts.Select(item => item.WithTimeOffset(timeOffset)))
                    : this;

            public override string ToString()
                => $"{(IsAnimated ? "Animated" : "Static")} RenderingContext[{_subContexts.Count}]";
        }

        sealed class NullRenderingContext : RenderingContext
        {
            public override bool IsAnimated => false;

            public override sealed RenderingContext WithOffset(Vector2 offset) => this;

            public override RenderingContext WithTimeOffset(double timeOffset) => this;

            public override string ToString() => "Null";
        }
    }
}
