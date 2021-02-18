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
            int inFrame,
            double inProgress,
            int outFrame,
            double outProgress)
            : base(name, propertyName, inFrame, inProgress)
        {
            OutFrame = outFrame;
            OutProgress = outProgress;
        }

        public int OutFrame { get; }

        public double OutProgress { get; }

        public string OutProgressText => $"{OutProgress:0.000#}";

        public double ConstrainedOutProgress => Math.Max(0, Math.Min(1, OutProgress));
    }
}
