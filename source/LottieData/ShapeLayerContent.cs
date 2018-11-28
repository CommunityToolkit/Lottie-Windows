// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    abstract class ShapeLayerContent : LottieObject
    {
        protected private ShapeLayerContent(string name, string matchName)
            : base(name)
        {
            MatchName = matchName;
        }

        public string MatchName { get; }

        /// <summary>
        /// Gets the <see cref="ShapeContentType"/> of the <see cref="ShapeLayerContent"/> object.
        /// </summary>
        public abstract ShapeContentType ContentType { get; }
    }
}
