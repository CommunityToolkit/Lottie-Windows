// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace LottieViewer.ViewModel
{
    class Marker
    {
        internal Marker(string name, string propertyName, double progress, string progressText)
        {
            Name = name;
            Progress = progress;
            ProgressText = progressText;
            PropertyName = propertyName;
        }

        public string PropertyName { get; }

        public string Name { get; }

        public double Progress { get; }

        public string ProgressText { get; }

        public double ConstrainedProgress => Math.Max(0, Math.Min(1, Progress));
    }
}
