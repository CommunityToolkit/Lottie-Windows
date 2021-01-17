// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
#if PUBLIC_Animatables
    public
#endif
    interface IAnimatableVector2 : IAnimatableValue<Vector2>
    {
        AnimatableVector2Type Type { get; }

        IAnimatableVector2 WithOffset(Vector2 offset);

        /// <summary>
        /// The animated value with each key frame offset by the given amount.
        /// </summary>
        /// <returns>The adjusted animated value.</returns>
        new IAnimatableVector2 WithTimeOffset(double timeOffset);
    }
}
