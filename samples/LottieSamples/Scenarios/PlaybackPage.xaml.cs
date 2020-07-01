// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace LottieSamples.Scenarios
{
    public sealed partial class PlaybackPage : Page
    {
        private bool wasPaused = false;
        
        public PlaybackPage()
        {
            this.InitializeComponent();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop the animation, which completes PlayAsync and resets to initial frame. 
            Playback_Player.Stop();
            Playback_Player.SetProgress(0);
            SetIsPlayingIndicator(false);
            wasPaused = false;
        }

        private void ReverseButton_Click(object sender, RoutedEventArgs e)
        {
            // Set reverse playback rate.
            // NOTE: This property is live, which means it takes effect even if the animation is playing.
            Playback_Player.PlaybackRate = -1;
            StartAnimation();
        }

        private async void StartAnimation()
        {
            if (!Playback_Player.IsPlaying)
            {
                // Play the animation at the currently specified playback rate.
                Playback_Player.PlaybackRate = 1;
                SetIsPlayingIndicator(true);
                await Playback_Player.PlayAsync(fromProgress: 0, toProgress: 1, looped: false);
                SetIsPlayingIndicator(false);
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            // Is the player playing and we did not get paused?
            if (Playback_Player.IsPlaying && !wasPaused)
            {
                Playback_Player.Pause();
                SetIsPlayingIndicator(false);
                wasPaused = true;
            }
            else
            {
                // Either not playing or paused
                // If paused, resume and set paused to false
                if (wasPaused)
                {
                    Playback_Player.Resume();
                    SetIsPlayingIndicator(true);
                }
                // Not playing, start animation now
                else
                {
                    StartAnimation();
                }
                wasPaused = false;
            }
        }

        private void SetIsPlayingIndicator(bool isPlaying)
        {
            if (isPlaying)
            {
                PlayIcon.Visibility = Visibility.Collapsed;
                PauseIcon.Visibility = Visibility.Visible;
                ToolTipService.SetToolTip(PlayPauseButton, "Pause");
                PlayPauseButton.SetValue(AutomationProperties.NameProperty, "Pause");
            }
            else
            {
                PlayIcon.Visibility = Visibility.Visible;
                PauseIcon.Visibility = Visibility.Collapsed;
                ToolTipService.SetToolTip(PlayPauseButton, "Play");
                PlayPauseButton.SetValue(AutomationProperties.NameProperty, "Play");
            }
        }
    }
}