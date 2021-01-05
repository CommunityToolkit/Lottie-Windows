// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
#if PUBLIC_IR
    public
#endif
    sealed class MergePaths : ShapeLayerContent
    {
        public MergePaths(
            in ShapeLayerContentArgs args,
            MergeMode mergeMode)
            : base(in args)
        {
            Mode = mergeMode;
        }

        public MergeMode Mode { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.MergePaths;

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
