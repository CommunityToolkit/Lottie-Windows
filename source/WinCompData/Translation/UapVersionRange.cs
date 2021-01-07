// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Translation
{
    /// <summary>
    /// Describes a range of UAP versions. A <see cref="Start"/> value of <c>null</c>
    /// indicates all versions less than or equal to the <see cref="End"/> value.
    /// An <see cref="End"/> value of <c>null</c> indicates all versions greater
    /// than or equal to the <see cref="Start"/> value. Both values being <c>null</c>
    /// indicates all versions.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    struct UapVersionRange
    {
        /// <summary>
        /// The start of the range, or <c>null</c> to indicate all values
        /// less than or equal to <see cref="End"/>.
        /// </summary>
        public uint? Start { get; set; }

        /// <summary>
        /// The end of the range, or <c>null</c> to indicate all values
        /// greater than or equal to <see cref="Start"/>.
        /// </summary>
        public uint? End { get; set; }

        public override string ToString()
        {
            if (Start.HasValue)
            {
                if (End.HasValue)
                {
                    if (End == Start)
                    {
                        return $"version {Start}";
                    }
                    else
                    {
                        return $"versions {Start}..{End}";
                    }
                }
                else
                {
                    return $"versions {Start}..";
                }
            }
            else if (End.HasValue)
            {
                return $"versions ..{End}";
            }

            return string.Empty;
        }

        // Convert ranges that Start at minimumVersion to ranges with no
        // Start. This ensures that we don't have 2 ways of expressing "all versions
        // that we support up to End". If we didn't do that, then (minimumVersion, n) and (null, n)
        // would effectively mean the same thing but it would be confusing to express
        // it in 2 different ways.
        public void NormalizeForMinimumVersion(uint minimumVersion)
        {
            // A Start of minimumVersion is the same as all versions up to End.
            if (Start == minimumVersion && (End is null || End > minimumVersion))
            {
                Start = null;
            }
        }
    }
}
