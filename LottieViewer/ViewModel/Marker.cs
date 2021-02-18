// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace LottieViewer.ViewModel
{
    class Marker
    {
        internal Marker(string name, string propertyName, int inFrame, double inProgress)
        {
            Name = name;
            InProgress = inProgress;
            InFrame = inFrame;
            PropertyName = propertyName;
        }

        public string PropertyName { get; }

        public string Name { get; }

        public int InFrame { get; }

        public double InProgress { get; }

        public string InProgressText => $"{InProgress:0.000#}";

        public double ConstrainedInProgress => Math.Max(0, Math.Min(1, InProgress));
    }
}
