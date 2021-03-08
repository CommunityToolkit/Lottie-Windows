// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace LottieViewer
{
    public sealed class ScrubberValueChangedEventArgs
    {
        internal ScrubberValueChangedEventArgs(double oldValue, double newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public double NewValue { get; }

        public double OldValue { get; }
    }
}
