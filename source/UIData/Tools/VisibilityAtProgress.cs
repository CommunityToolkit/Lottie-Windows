// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    readonly struct VisibilityAtProgress
    {
        internal VisibilityAtProgress(bool isVisible, float progress)
            => (IsVisible, Progress) = (isVisible, progress);

        internal bool IsVisible { get; }

        internal float Progress { get; }

        internal void Deconstruct(out bool isVisible, out float progress)
        {
            isVisible = IsVisible;
            progress = Progress;
        }

        // Composes 2 list sequences of VisibilityAtProgress by ANDing them together.
        // The resulting sequence is initially invisible, then visible when both a and
        // b are visible, and invisible if either a or b are invisible.
        internal static IEnumerable<VisibilityAtProgress> Compose(
            VisibilityAtProgress[] a,
            VisibilityAtProgress[] b)
        {
            // Get the visibilities in order, with any redundant visibilities removed.
            var items = SanitizeSequence(a).Concat(SanitizeSequence(b)).OrderBy(v => v.Progress).ThenBy(v => !v.IsVisible).ToArray();

            // The output is visible any time both a and b are visible at the same time.
            var visibilityCounter = 0;

            var initialInvisibilityOutput = false;

            foreach (var item in items)
            {
                visibilityCounter += item.IsVisible ? 1 : -1;
                if (visibilityCounter == 2)
                {
                    // Both a and b are now visible.
                    if (!initialInvisibilityOutput)
                    {
                        initialInvisibilityOutput = true;
                        if (item.Progress != 0)
                        {
                            yield return new VisibilityAtProgress(false, 0);
                        }
                    }

                    yield return new VisibilityAtProgress(true, item.Progress);
                }
                else if (visibilityCounter == 1 && !item.IsVisible)
                {
                    // We were visible, but now we're not.
                    yield return new VisibilityAtProgress(false, item.Progress);
                }
            }
        }

        // Orders the items in the sequence by Progress, and removes any redundant items.
        static IEnumerable<VisibilityAtProgress> SanitizeSequence(IEnumerable<VisibilityAtProgress> sequence)
        {
            // Sequences start implicitly invisible.
            var previousVisibility = false;

            foreach (var item in sequence.OrderBy(v => v.Progress).ThenBy(v => !v.IsVisible))
            {
                // Ignore any repeats of the same visibility state.
                if (previousVisibility != item.IsVisible)
                {
                    yield return item;
                    previousVisibility = item.IsVisible;
                }
            }
        }

        public override string ToString() => $"{(IsVisible ? "Visible" : "Invisible")} @ {Progress}";
    }
}
