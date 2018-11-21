// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class MergePaths : ShapeLayerContent
    {
        public MergePaths(
            string name,
            string matchName,
            MergeMode mergeMode)
            : base(name, matchName)
        {
            Mode = mergeMode;
        }

        public MergeMode Mode { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.MergePaths;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.MergePaths;

        public enum MergeMode
        {
            Merge,
            Add,
            Subtract,
            Intersect,
            ExcludeIntersections,
        }
    }
}
