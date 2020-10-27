// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

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
        readonly IReadOnlyList<Mask> _masks = Array.Empty<Mask>();

        private protected Layer(in LayerArgs args)
            : base(args.Name)
        {
            Index = args.Index;
            Parent = args.Parent;
            IsHidden = args.IsHidden;
            Transform = args.Transform;
            TimeStretch = args.TimeStretch;
            StartTime = args.StartFrame;
            InPoint = args.InFrame;
            OutPoint = args.OutFrame;
            BlendMode = args.BlendMode;
            Is3d = args.Is3d;
            AutoOrient = args.AutoOrient;
            LayerMatteType = args.LayerMatteType;

            if (args.Masks != null)
            {
                _masks = args.Masks.ToArray();
            }
        }

        public bool AutoOrient { get; }

        public bool IsHidden { get; }

        public BlendMode BlendMode { get; }

        /// <summary>
        /// Gets the frame at which this <see cref="Layer"/> starts playing. May be negative.
        /// </summary>
        /// <remarks><see cref="Layer"/>s all start together.</remarks>
        public double StartTime { get; }

        /// <summary>
        /// Gets the frame at which this <see cref="Layer"/> becomes visible. <see cref="OutPoint"/>.
        /// </summary>
        public double InPoint { get; }

        /// <summary>
        /// Gets the frame at which this <see cref="Layer"/> becomes invisible. <see cref="OutPoint"/>.
        /// </summary>
        public double OutPoint { get; }

        public double TimeStretch { get; }

        public abstract LayerType Type { get; }

        public Transform Transform { get; }

        public bool Is3d { get; }

        /// <summary>
        /// Gets the index used to uniquely identify a <see cref="Layer"/> within the owning <see cref="LayerCollection"/>.
        /// </summary>
        internal int Index { get; }

        /// <summary>
        /// Gets the index that identifies the index of the <see cref="Layer"/> from which transforms are inherited,
        /// or null if no transforms are inherited.
        /// </summary>
        public int? Parent { get; }

        /// <summary>
        /// Gets the list of masks appplied to the layer.
        /// </summary>
        public IReadOnlyList<Mask> Masks => _masks;

        public MatteType LayerMatteType { get; }

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

            public IEnumerable<Mask>? Masks { get; set; }

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
