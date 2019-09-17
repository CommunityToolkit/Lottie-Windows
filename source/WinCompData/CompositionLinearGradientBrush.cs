﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(5)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionLinearGradientBrush : CompositionGradientBrush
    {
        internal CompositionLinearGradientBrush()
        {
        }

        public Vector2? EndPoint { get; set; }

        public Vector2? StartPoint { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionLinearGradientBrush;
    }
}
