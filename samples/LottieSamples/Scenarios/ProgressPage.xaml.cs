// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace LottieSamples.Scenarios
{
    public sealed partial class ProgressPage : Page
    {
        private const double ClickedSegmentFromProgress = 0.69;

        public ProgressPage()
        {
            this.InitializeComponent();

            // Set Lottie Source.
            InitializeJsonSource();
        }

        private async void InitializeJsonSource()
        {
            // We'll need to set the JSON Source in code-behind in order to await it ...
            await Progress_Source.SetSourceAsync(new Uri("ms-appx:///Assets/LightBulb.json"));

            // ... so that when the SetSourceAsync completes, we can enable the Switch + Slider for user interaction.
            LightToggle.IsEnabled = ProgressSlider.IsEnabled = true;
        }

        private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            // Use slider value to set Progress (current frame / total frames).
            var progress = e.NewValue;
            Progress_Player.SetProgress(progress);

            // Reset toggle switch if Progress crosses segment threshold.
            LightToggle.IsOn = progress > ClickedSegmentFromProgress;
        }

        private void LightToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // Toggle On => frame at progress 1.0
            // Toggle Off => frame at progress 0.0
            // Progress [0.0, 0.69] => Toggle On
            // Progress (0.69, 1.0] => Toggle Off
            var progress = ProgressSlider.Value;
            var value = LightToggle.IsOn
                ? (progress > ClickedSegmentFromProgress ? progress : 1)
                : (progress <= ClickedSegmentFromProgress ? progress : 0);

            if (value == 1.0 || value == 0.0)
            {
                Progress_Player.SetProgress(value);
                ProgressSlider.Value = value;
            }
        }
    }
}