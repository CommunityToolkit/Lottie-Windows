// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using LottieViewer.ViewModel;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LottieViewer
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// This is where the Lottie file is displayed. This is a wrapper around the
    /// AnimatedVisualPlayer that plays a loading animation and exposes the
    /// diagnostics object as a view model.
    /// </summary>
    public sealed partial class Stage : UserControl
    {
        // The color of the artboard is a dependency property so that it can be the
        // target of binding.
        public static readonly DependencyProperty ArtboardColorProperty =
            DependencyProperty.Register("ArtboardColor", typeof(Color), typeof(Stage), new PropertyMetadata(Colors.Black));

        public Stage()
        {
            this.InitializeComponent();

            Reset();
        }

        // The DiagnosticsViewModel contains information about the currently playing
        // Lottie file. This information is consumed by other controls such as the
        // color picker and scrubber.
        internal LottieVisualDiagnosticsViewModel DiagnosticsViewModel => _diagnosticsViewModel;

        internal AnimatedVisualPlayer Player => _player;

        public Color ArtboardColor
        {
            get { return (Color)GetValue(ArtboardColorProperty); }
            set { SetValue(ArtboardColorProperty, value); }
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100

        // Use "Async" suffix for async methods
#pragma warning disable VSTHRD200
        internal async void PlayFileAsync(StorageFile file)
#pragma warning restore VSTHRD200
#pragma warning restore VSTHRD100
        {
            var startDroppedAnimation = _feedbackLottie.PlayDroppedAnimationAsync();

            _player.Opacity = 0;
            try
            {
                // Load the Lottie composition.
                await _playerSource.SetSourceAsync(file);
            }
            catch (Exception)
            {
                // Failed to load.
                _player.Opacity = 1;
                try
                {
                    await _feedbackLottie.PlayLoadFailedAnimationAsync();
                }
                catch
                {
                    // Ignore PlayLoadFailedAnimationAsync exceptions so they don't crash the process.
                }

                return;
            }

            // Wait until the dropping animation has finished.
            await startDroppedAnimation;

            _player.Opacity = 1;
            try
            {
                await _player.PlayAsync(0, 1, true);
            }
            catch
            {
                // Ignore PlayAsync exceptions so they don't crash the process.
            }
        }

        internal void DoDragEnter()
        {
            _feedbackLottie.PlayDragEnterAnimation();
        }

        internal void DoDragDropped(StorageFile file)
        {
            PlayFileAsync(file);
        }

        internal void DoDragLeave()
        {
            _feedbackLottie.PlayDragLeaveAnimation();
        }

        internal void Reset()
        {
            _feedbackLottie.PlayInitialStateAnimation();
        }
    }
}
