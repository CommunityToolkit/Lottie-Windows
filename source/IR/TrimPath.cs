// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.Animatables;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.IR
{
    /// <summary>
    /// Describes how a path is to be trimmed.
    /// </summary>
#if PUBLIC_IR
    public
#endif
    sealed class TrimPath : ShapeLayerContent
    {
        public TrimPath(
            in ShapeLayerContentArgs args,
            TrimType trimPathType,
            Animatable<Trim> start,
            Animatable<Trim> end,
            Animatable<Rotation> offset)
            : base(in args)
        {
            TrimPathType = trimPathType;
            Start = start;
            End = end;
            Offset = offset;
        }

        public Animatable<Trim> Start { get; }

        public Animatable<Trim> End { get; }

        public Animatable<Rotation> Offset { get; }

        public TrimType TrimPathType { get; }

        /// <inheritdoc/>
        public override ShapeContentType ContentType => ShapeContentType.TrimPath;

        /// <summary>
        /// Returns a new <see cref="TrimPath"/> that trims in the reverse direction of this
        /// <see cref="TrimPath"/>.
        /// </summary>
        /// <returns>A new <see cref="TrimPath"/> that trims in the reverse direction.</returns>
        public TrimPath CloneWithReversedDirection()
        {
            // Start = 1 - end
            var start = End.Select(trim => Trim.FromPercent(100 - trim.Percent));

            // End = 1 - start
            var end = Start.Select(trim => Trim.FromPercent(100 - trim.Percent));

            // Offset = offset * -1
            var offset = Offset.Select(rotation => Rotation.FromDegrees(rotation.Degrees * -1));

            return new TrimPath(
                new ShapeLayerContentArgs { BlendMode = BlendMode, MatchName = MatchName, Name = Name },
                TrimPathType,
                start,
                end,
                offset);
        }

        public enum TrimType
        {
            Simultaneously,
            Individually,
        }
    }
}
