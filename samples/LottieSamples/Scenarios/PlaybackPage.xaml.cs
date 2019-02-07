// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LottieSamples.Scenarios
{
    public sealed partial class PlaybackPage : Page
    {
        public PlaybackPage()
        {
            this.InitializeComponent();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Set forward playback rate.
            // NOTE: This property is live, which means it takes effect even if the animation is playing.
            Playback_Player.PlaybackRate = 1;
            StartPlaying();
        }

        private void PauseButton_Checked(object sender, RoutedEventArgs e)
        {
            // Pause the animation, if playing.
            // NOTE: Pausing does not cause PlayAsync to complete.
            Playback_Player.Pause();
        }

        private void PauseButton_Unchecked(object sender, RoutedEventArgs e)
        {
            // Resume playing current animation.
            Playback_Player.Resume();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop the animation, which completes PlayAsync and resets to initial frame. 
            Playback_Player.Stop();
            PauseButton.IsChecked = false;
        }

        private void ReverseButton_Click(object sender, RoutedEventArgs e)
        {
            // Set reverse playback rate.
            // NOTE: This property is live, which means it takes effect even if the animation is playing.
            Playback_Player.PlaybackRate = -1;
            StartPlaying();
        }

        private void StartPlaying()
        {
            // If already playing, keep playing till PlayAsync completes or is interrupted.
            if (Playback_Player.IsPlaying && !(bool)PauseButton.IsChecked)
            {
                return;
            }

            // Resume playing the animation, if paused.
            if ((bool)PauseButton.IsChecked)
            {
                PauseButton.IsChecked = false;
            }
            else
            {
                // Play the animation at the currently specified playback rate.
                _ = Playback_Player.PlayAsync(fromProgress: 0, toProgress: 1, looped: false);
            }
        }
    }
}