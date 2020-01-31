// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class TrimPath : ShapeLayerContent
    {
        public TrimPath(
            in ShapeLayerContentArgs args,
            TrimType trimPathType,
            Animatable<double> startPercent,
            Animatable<double> endPercent,
            Animatable<Rotation> offset)
            : base(in args)
        {
            TrimPathType = trimPathType;
            StartPercent = startPercent;
            EndPercent = endPercent;
            Offset = offset;
        }

        public Animatable<double> StartPercent { get; }

        public Animatable<double> EndPercent { get; }

        public Animatable<Rotation> Offset { get; }

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
