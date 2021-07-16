// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
    /// <summary>
    /// Base class for layer objects.
    /// </summary>
    /// <remarks>
    /// Each <see cref="Layer"/>, apart from the root <see cref="PreCompLayer"/> belongs to a <see cref="PreCompLayer"/> and has
    /// an index that determines its rendering order, and is also used to identify it as the owner of a set of transforms that
    /// can be inherited by other <see cref="Layer"/>s.</remarks>
#if PUBLIC_LottieData
    public
#endif
    abstract class Layer : LottieObject
    {
        private protected Layer(in LayerArgs args)
            : base(args.Name)
        {
            AutoOrient = args.AutoOrient;
            BlendMode = args.BlendMode;
            Effects = args.Effects;
            Index = args.Index;
            InPoint = args.InFrame;
            Is3d = args.Is3d;
            IsHidden = args.IsHidden;
            LayerMatteType = args.LayerMatteType;
            Masks = args.Masks;
            OutPoint = args.OutFrame;
            Parent = args.Parent;
            StartTime = args.StartFrame;
            TimeStretch = args.TimeStretch;
            Transform = args.Transform;
        }

        public bool AutoOrient { get; }

        public BlendMode BlendMode { get; }

        public IReadOnlyList<Effect> Effects { get; }

        /// <summary>
        /// Gets the index used to uniquely identify a <see cref="Layer"/> within the owning <see cref="LayerCollection"/>.
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// Gets the frame at which this <see cref="Layer"/> becomes visible. <see cref="OutPoint"/>.
        /// </summary>
        public double InPoint { get; }

        public bool Is3d { get; }

        public bool IsHidden { get; }

        public MatteType LayerMatteType { get; }

        /// <summary>
        /// Gets the list of masks appplied to the layer.
        /// </summary>
        public IReadOnlyList<Mask> Masks { get; }

        /// <summary>
        /// Gets the frame at which this <see cref="Layer"/> becomes invisible. <see cref="OutPoint"/>.
        /// </summary>
        public double OutPoint { get; }

        /// <summary>
        /// Gets the index that identifies the index of the <see cref="Layer"/> from which transforms are inherited,
        /// or null if no transforms are inherited.
        /// </summary>
        public int? Parent { get; set; }

        /// <summary>
        /// Gets the frame at which this <see cref="Layer"/> starts playing. May be negative.
        /// </summary>
        /// <remarks><see cref="Layer"/>s all start together.</remarks>
        public double StartTime { get; }

        public double TimeStretch { get; }

        public Transform Transform { get; }

        public abstract LayerType Type { get; }

        /// <inheritdoc/>
        public override sealed LottieObjectType ObjectType => LottieObjectType.Layer;

        public ref struct LayerArgs
        {
            public string Name { get; set; }

            public int Index { get; set; }

            public int? Parent { get; set; }

            public bool IsHidden { get; set; }

            public IReadOnlyList<Effect> Effects { get; set; }

            public Transform Transform { get; set; }

            public double TimeStretch { get; set; }

            public double StartFrame { get; set; }

            public double InFrame { get; set; }

            public double OutFrame { get; set; }

            public BlendMode BlendMode { get; set; }

            public bool Is3d { get; set; }

            public bool AutoOrient { get; set; }

            public IReadOnlyList<Mask> Masks { get; set; }

            public MatteType LayerMatteType { get; set; }
        }

        public enum LayerType
        {
            PreComp,
            Solid,
            Image,
            Null,
            Shape,
            Text,
        }

        public enum MatteType
        {
            None = 0,
            Add,
            Invert,
        }
    }
}
