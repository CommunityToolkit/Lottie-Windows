// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace LottieViewer.ViewModel
{
    // A marker that has a non-0 duration.
    sealed class MarkerWithDuration : Marker
    {
        public double ToProgress { get; set; }

        public string ToProgressText { get; set; }

        public double ConstrainedToProgress => Math.Max(0, Math.Min(1, ToProgress));
    }
}
