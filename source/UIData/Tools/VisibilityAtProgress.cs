// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
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

        // Composes 2 lists of VisibilityAtProgress by ANDing them together.
        // The resulting sequence always has a value at progress 0. There will
        // be no consective items with the same visibility (i.e. each item describes
        // a change of visibility state). The visibility will be visibile when
        // both a AND b are visible, and invisible if either a OR b is invisible.
        internal static IEnumerable<VisibilityAtProgress> Compose(
            VisibilityAtProgress[] a,
            VisibilityAtProgress[] b)
        {
            // Get the visibilities in order, with any redundant visibilities removed.
            var items = SanitizeSequence(a).Concat(SanitizeSequence(b)).OrderBy(v => v.Progress).ThenBy(v => !v.IsVisible).ToArray();

            // The output is visible any time both a and b are visible at the same time.
            var visibilityCounter = 0;

            var has0ProgressBeenOutput = false;

            foreach (var item in items)
            {
                visibilityCounter += item.IsVisible ? 1 : -1;
                if (visibilityCounter == 2)
                {
                    // Both a and b are now visible.
                    if (!has0ProgressBeenOutput)
                    {
                        // If we haven't output a value for progress 0, and the current
                        // item isn't for 0, output a progress 0 value now.
                        if (item.Progress != 0)
                        {
                            yield return new VisibilityAtProgress(false, 0);
                        }

                        has0ProgressBeenOutput = true;
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

            foreach (var item in sequence.GroupBy(v => v.Progress).OrderBy(v => v.Key))
            {
                // Do not allow multiple visibilities at the same progress. It should
                // never happen, and if it did it's not clear what it means.
                var group = item.ToArray();
                if (group.Length > 1)
                {
                    throw new ArgumentException();
                }

                var visibility = group[0];

                // Ignore any repeats of the same visibility state.
                if (previousVisibility != visibility.IsVisible)
                {
                    yield return visibility;
                    previousVisibility = visibility.IsVisible;
                }
            }
        }

        public override string ToString() => $"{(IsVisible ? "Visible" : "Invisible")} @ {Progress}";
    }
}
