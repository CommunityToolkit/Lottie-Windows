// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if !WINDOWS_UWP
    public
#endif
    sealed class TrimPath : ShapeLayerContent
    {
        public TrimPath(
            string name,
            string matchName,
            TrimType trimPathType,
            Animatable<double> startPercent,
            Animatable<double> endPercent,
            Animatable<double> offsetDegrees)
            : base(name, matchName)
        {
            TrimPathType = trimPathType;
            StartPercent = startPercent;
            EndPercent = endPercent;
            OffsetDegrees = offsetDegrees;
        }

        public Animatable<double> StartPercent { get; }

        public Animatable<double> EndPercent { get; }

        public Animatable<double> OffsetDegrees { get; }

        public TrimType TrimPathType { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.TrimPath;

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.TrimPath;

        public enum TrimType
        {
            Simultaneously,
            Individually,
        }
    }
}
