// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class ShapeLayerContent : LottieObject
    {
        private protected ShapeLayerContent(in ShapeLayerContentArgs args)
            : base(args.Name)
        {
            BlendMode = args.BlendMode;
            MatchName = args.MatchName;
        }

        public BlendMode BlendMode { get; }

        public string MatchName { get; }

        /// <summary>
        /// Gets the <see cref="ShapeContentType"/> of the <see cref="ShapeLayerContent"/> object.
        /// </summary>
        public abstract ShapeContentType ContentType { get; }

        public override sealed LottieObjectType ObjectType => LottieObjectType.ShapeLayerContent;

        public ref struct ShapeLayerContentArgs
        {
            public string Name { get; set; }

            public string MatchName { get; set; }

            public BlendMode BlendMode { get; set; }
        }

        public ShapeLayerContentArgs CopyArgs()
        {
            return new ShapeLayerContentArgs
            {
                Name = Name,
                MatchName = MatchName,
                BlendMode = BlendMode,
            };
        }

        /// <summary>
        /// Make a copy of the shape layer content and offset all frames by some value.
        /// </summary>
        /// <param name="offset">Offset value.</param>
        /// <returns>Copy of shape layer content with offsetted frames.</returns>
        public abstract ShapeLayerContent WithTimeOffset(double offset);
    }
}
