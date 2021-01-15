// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContents;
using Microsoft.Toolkit.Uwp.UI.Lottie.IR.RenderingContexts;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    sealed class Rendering
    {
        internal Rendering(RenderingContent content, RenderingContext context)
        {
            Content = content;
            Context = context;
        }

        public RenderingContent Content { get; }

        public RenderingContext Context { get; }

        public bool IsAnimated => Content.IsAnimated || Context.IsAnimated;

        public Rendering WithContext(RenderingContext replacementContext)
            => new Rendering(Content, replacementContext);

        public void Deconstruct(out RenderingContent content, out RenderingContext context)
        {
            content = Content;
            context = Context;
        }

        public static Rendering UnifyTimebase(Rendering rendering, double timeOffset)
        {
            // Get the accumulated time offsets from the context.
            var contentOffset = timeOffset + rendering.Context.
                                                OfType<TimeOffsetRenderingContext>().
                                                Sum(ctx => ctx.TimeOffset);

            // Convert the context to a single timebase.
            var adjustedContext = contentOffset == 0
                                    ? rendering.Context
                                    : RenderingContext.Compose(UnifyTimebase(rendering.Context));
            var adjustedContent = rendering.Content.WithTimeOffset(contentOffset);
            return new Rendering(adjustedContent, adjustedContext);
        }

        static IEnumerable<RenderingContext> UnifyTimebase(RenderingContext context)
        {
            var timeOffset = 0.0;

            foreach (var subContext in context)
            {
                switch (subContext)
                {
                    case TimeOffsetRenderingContext timeOffsetContext:
                        timeOffset += timeOffsetContext.TimeOffset;
                        break;
                    default:
                        if (subContext.IsAnimated)
                        {
                            yield return subContext.WithTimeOffset(timeOffset);
                        }
                        else
                        {
                            yield return subContext;
                        }

                        break;
                }
            }
        }

        public override string ToString() => $"{(IsAnimated ? "Animated" : "Static")} ({Content}, {Context})";
    }
}