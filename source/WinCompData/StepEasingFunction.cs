// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(3)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class StepEasingFunction : CompositionEasingFunction
    {
        internal StepEasingFunction(int steps)
        {
            StepCount = steps;

            // TODO - setting the FinalStep here is necessary if it's not set
            //        explicitly, but the real Comp object doesn't seem to do this... why?
            FinalStep = steps;
        }

        // Default is 1.
        public int? StepCount { get; set; }

        // Default is false.
        public bool? IsInitialStepSingleFrame { get; set; }

        // Default is 0.
        public int? InitialStep { get; set; }

        // Default is 1.
        public int? FinalStep { get; set; }

        // Default is false.
        public bool? IsFinalStepSingleFrame { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.StepEasingFunction;
    }
}
