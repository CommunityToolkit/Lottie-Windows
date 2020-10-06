// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System;

namespace LottieViewer.ViewModel
{
    class Marker
    {
        public string PropertyName { get; set; }

        public string Name { get; set; }

        public double Progress { get; set; }

        public string ProgressText { get; set; }

        public double ConstrainedProgress => Math.Max(0, Math.Min(1, Progress));
    }
}
