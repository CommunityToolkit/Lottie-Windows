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
        private bool _isPaused = false;

        public PlaybackPage()
        {
            this.InitializeComponent();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop the animation, which completes PlayAsync and resets to initial frame. 
            Playback_Player.Stop();
            SetIsPlayingIndicator(false);
            SetDirectionIndicator(false);
            _isPaused = false;
        }

        /// <summary>
        /// Toggles the "direction" in which the animation is playing and updates the buttons icon accordingly.
        /// The animation can either play forwards or in reverse
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DirectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (ForwardArrow.Visibility == Visibility.Visible)
            {
                // Forward arrow was visible, that means that previous mode was reverse.
                // Set normal playback rate.
                // NOTE: This property is live, which means it takes effect even if the animation is playing.
                Playback_Player.PlaybackRate = 1;
                SetDirectionIndicator(false);
            }
            else
            {
                // Set reverse playback rate.
                // NOTE: This property is live, which means it takes effect even if the animation is playing.
                Playback_Player.PlaybackRate = -1;
                SetDirectionIndicator(true);
            }
        }

        private void SetDirectionIndicator(bool isReverse)
        {
            if (isReverse)
            {
                BackwardArrow.Visibility = Visibility.Collapsed;
                ForwardArrow.Visibility = Visibility.Visible;
                ToolTipService.SetToolTip(DirectionButton, "Forwards");
                DirectionButton.SetValue(AutomationProperties.NameProperty, "Set playback direction to forwards");
            }
            else
            {
                BackwardArrow.Visibility = Visibility.Visible;
                ForwardArrow.Visibility = Visibility.Collapsed;
                ToolTipService.SetToolTip(DirectionButton, "Reverse");
                DirectionButton.SetValue(AutomationProperties.NameProperty, "Set playback direction to reverse");
            }
        }

        private async void EnsurePlaying()
        {
            if (!Playback_Player.IsPlaying)
            {
                // Play the animation at the currently specified playback rate.
                Playback_Player.PlaybackRate = 1;
                SetIsPlayingIndicator(true);
                await Playback_Player.PlayAsync(fromProgress: 0, toProgress: 1, looped: false);
                if (!Playback_Player.IsPlaying)
                {
                    SetIsPlayingIndicator(false);
                    SetDirectionIndicator(false);
                }
            }
        }

        /// <summary>
        /// This handles the Play/Pause button click. There are three states in which this method can get called:
        /// 1. Animation not playing and not started.
        /// 2. Animation playing
        /// 3. Animation paused
        ///
        /// For all of these cases, this function will either resume/start playing the animation or pause the animation and update the buttons icon accordingly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            // Is the player playing and we did not get paused?
            if (Playback_Player.IsPlaying && !_isPaused)
            {
                Playback_Player.Pause();
                SetIsPlayingIndicator(false);
                _isPaused = true;
            }
            else
            {
                // Either not playing or paused.
                // If paused, resume and set paused to false.
                if (_isPaused)
                {
                    Playback_Player.Resume();
                    SetIsPlayingIndicator(true);
                }
                else
                {
                    // Not playing, start animation now.
                    EnsurePlaying();
                }
                _isPaused = false;
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