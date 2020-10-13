// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace LottieViewer.ViewModel
{
    // A marker that has a non-0 duration.
    sealed class MarkerWithDuration : Marker
    {
        internal MarkerWithDuration(
            string name,
            string propertyName,
            double progress,
            string progressText,
            double toProgress,
            string toProgressText)
            : base(name, propertyName, progress, progressText)
        {
            ToProgress = toProgress;
            ToProgressText = toProgressText;
        }

        public double ToProgress { get; }

        public string ToProgressText { get; }

        public double ConstrainedToProgress => Math.Max(0, Math.Min(1, ToProgress));
    }
}
