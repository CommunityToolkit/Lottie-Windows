﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    interface IGradient
    {
        IAnimatableVector3 StartPoint { get; }

        IAnimatableVector3 EndPoint { get; }

        Animatable<Sequence<ColorGradientStop>> ColorStops { get; }

        Animatable<Sequence<OpacityGradientStop>> OpacityPercentStops { get; }
    }
}
