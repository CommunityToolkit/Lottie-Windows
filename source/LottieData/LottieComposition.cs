// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class LottieComposition : LottieObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LottieComposition"/> class.
        /// </summary>
        /// <param name="name">The name of the composition.</param>
        /// <param name="width">Width of animation canvas as specified in AfterEffects.</param>
        /// <param name="height">Height of animation canvas as specified in AfterEffects.</param>
        /// <param name="inPoint">Frame at which animation begins as specified in AfterEffects.</param>
        /// <param name="outPoint">Frame at which animation ends as specified in AfterEffects.</param>
        /// <param name="framesPerSecond">FrameRate (frames per second) at which animation data was generated in AfterEffects.</param>
        /// <param name="is3d">True if the composition is 3d.</param>
        /// <param name="version">The version of the schema of the composition.</param>
        /// <param name="assets">Assets that are part of the composition.</param>
        /// <param name="chars">Character definitions that are part of the composition.</param>
        /// <param name="fonts">Font definitions that are part of the composition.</param>
        /// <param name="layers">The layers in the composition.</param>
        /// <param name="markers">Markers that define named portions of the composition.</param>
        public LottieComposition(
            string name,
            double width,
            double height,
            double inPoint,
            double outPoint,
            double framesPerSecond,
            bool is3d,
            Version version,
            AssetCollection assets,
            IEnumerable<Char> chars,
            IEnumerable<Font> fonts,
            LayerCollection layers,
            IEnumerable<Marker> markers)
            : base(name)
        {
            Is3d = is3d;
            Width = width;
            Height = height;
            InPoint = inPoint;
            OutPoint = outPoint;
            FramesPerSecond = framesPerSecond;
            Duration = TimeSpan.FromSeconds((outPoint - inPoint) / framesPerSecond);
            Version = version;
            Layers = layers;
            Assets = assets;
            Chars = chars.ToArray();
            Fonts = fonts.ToArray();
            Markers = markers.ToArray();
        }

        public bool Is3d { get; }

        public double FramesPerSecond { get; }

        public double Width { get; }

        public double Height { get; }

        /// <summary>
        /// Gets the frame at which the animation begins.
        /// </summary>
        public double InPoint { get; }

        /// <summary>
        /// Gets the frame at which the animation ends.
        /// </summary>
        public double OutPoint { get; }

        public IEnumerable<Char> Chars { get; }

        public IEnumerable<Font> Fonts { get; }

        public IEnumerable<Marker> Markers { get; }

        public TimeSpan Duration { get; }

        public AssetCollection Assets { get; }

        public LayerCollection Layers { get; }

        /// <inheritdoc/>
        public override LottieObjectType ObjectType => LottieObjectType.LottieComposition;

        /// <summary>
        /// Gets the Lottie version.
        /// </summary>
        public Version Version { get; }
    }
}
