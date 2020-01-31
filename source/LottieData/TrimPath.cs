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
            Animatable<Trim> startTrim,
            Animatable<Trim> endTrim,
            Animatable<Rotation> offset)
            : base(in args)
        {
            TrimPathType = trimPathType;
            StartTrim = startTrim;
            EndTrim = endTrim;
            Offset = offset;
        }

        public Animatable<Trim> StartTrim { get; }

        public Animatable<Trim> EndTrim { get; }

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
