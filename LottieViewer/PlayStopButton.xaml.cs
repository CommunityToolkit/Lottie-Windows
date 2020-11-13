// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using AnimatedVisuals;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LottieViewer
{
    /// <summary>
    /// Animated button for playing or stopping an animtion.
    /// </summary>
    public sealed partial class PlayStopButton : UserControl
    {
        // True iff we should use the animated play-stop button.
        static readonly bool IsAnimationSupported =
            Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 11);

        bool _isHoveredOver;

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(
                "IsChecked",
                typeof(bool),
                typeof(PlayStopButton),
                new PropertyMetadata(false, OnIsCheckedChanged));

        public event RoutedEventHandler? Toggled;

        public PlayStopButton()
        {
            this.InitializeComponent();

            // Hide the animated or the non-animated view, depending on whether we are on
            // a recent-enough OS to support this animations.
            if (IsAnimationSupported)
            {
                _playStopPlayer.Visibility = Visibility.Visible;
                _fallbackGrid.Visibility = Visibility.Collapsed;

                // Allow color changing when the enabled state changes.
                IsEnabledChanged += PlayStopButton_IsEnabledChanged;
            }
            else
            {
                _playStopPlayer.Visibility = Visibility.Collapsed;
                _fallbackGrid.Visibility = Visibility.Visible;
                _playText.Visibility = Visibility.Visible;
                _stopText.Visibility = Visibility.Collapsed;
            }

            // Subscribe to events so we can animate pointer enter and exit.
            PointerEntered += PlayStopButton_PointerEntered;
            PointerExited += PlayStopButton_PointerExited;
        }

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        void PlayStopButton_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isHoveredOver = true;
            AnimateScale(new Vector3(0.8F));
            SetAnimatedVisualColor();
        }

        void PlayStopButton_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            _isHoveredOver = false;
            AnimateScale(Vector3.One);
            SetAnimatedVisualColor();
        }

        static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((PlayStopButton)d).OnIsCheckedChanged(e);

#pragma warning disable VSTHRD100 // Avoid async void methods
        async void OnIsCheckedChanged(DependencyPropertyChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            var isChecked = (bool)e.NewValue;

            // Keep the toggle button in sync with the control's IsChecked property.
            _playStopButton.IsChecked = isChecked;

            if (IsAnimationSupported)
            {
                // Play the segment that either goes from Play to Stop or Stop to Play.
                var startProgress = isChecked ? LottieViewer_04_Playback.M_Play_Click_On : LottieViewer_04_Playback.M_Stop_Click_On;
                var endProgress = isChecked ? LottieViewer_04_Playback.M_Play_Click_Off : LottieViewer_04_Playback.M_Stop_Click_Off;

                try
                {
                    // Play the animation.
                    await _playStopPlayer.PlayAsync(startProgress, endProgress, looped: false);
                }
                catch
                {
                    // Swallow any exceptions from PlayAsync.
                }
            }
            else
            {
                if (isChecked)
                {
                    _playText.Visibility = Visibility.Collapsed;
                    _stopText.Visibility = Visibility.Visible;
                }
                else
                {
                    _playText.Visibility = Visibility.Visible;
                    _stopText.Visibility = Visibility.Collapsed;
                }
            }
        }

        void AnimateScale(Vector3 scale)
        {
            var compositor = Window.Current.Compositor;
            var animation = compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(1.0f, scale);
            animation.Duration = TimeSpan.FromSeconds(0.333);
            animation.Target = "Scale";
            _playStopPlayerContainer.StartAnimation(animation);
        }

        void PlayControl_Toggled(object sender, RoutedEventArgs e)
        {
            // Keep our IsChecked property in sync with the ToggleButton's IsChecked property.
            IsChecked = _playStopButton.IsChecked == true;

            // Notify listeners.
            Toggled?.Invoke(sender, e);
        }

        void PlayStopButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetAnimatedVisualColor();
        }

        void SetAnimatedVisualColor()
        {
            var colorChoice =
                _playStopButton.IsEnabled
                    ? (_isHoveredOver ? ColorState.IsEnabledHover : ColorState.IsEnabled)
                    : ColorState.Disabled;

            SetAnimatedVisualColor(colorChoice);
        }

        void SetAnimatedVisualColor(ColorState colorChoice)
        {
            _animatedVisual.Foreground = colorChoice switch
            {
                ColorState.IsEnabled => (Color)Application.Current.Resources["LottieBasic"],
                ColorState.IsEnabledHover => (Color)Application.Current.Resources["ForegroundColor"],
                ColorState.Disabled => (Color)Application.Current.Resources["DisabledColor"],
                _ => throw new InvalidOperationException(),
            };
        }

        enum ColorState
        {
            IsEnabled,
            IsEnabledHover,
            Disabled,
        }
    }
}
