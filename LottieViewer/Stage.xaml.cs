// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LottieViewer
{
    /// <summary>
    /// Stage.
    /// </summary>
    public sealed partial class Stage : UserControl
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static readonly DependencyProperty ArtboardColorProperty =
            DependencyProperty.Register(nameof(ArtboardColor), typeof(Color), typeof(Stage), new PropertyMetadata(Colors.White));

        public static readonly DependencyProperty PlayerProperty =
            DependencyProperty.Register(nameof(Player), typeof(AnimatedVisualPlayer), typeof(Stage), new PropertyMetadata(null));

        public static readonly DependencyProperty PlayerHasIssuesProperty =
            DependencyProperty.Register(nameof(PlayerHasIssues), typeof(bool), typeof(Stage), new PropertyMetadata(false));

        public static readonly DependencyProperty PlayerIssuesProperty =
            DependencyProperty.Register(nameof(PlayerIssues), typeof(Issue[]), typeof(Stage), new PropertyMetadata(null));

        public Stage()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            this.InitializeComponent();
            SetValue(PlayerProperty, _player);

            // Subscribe to events from the player so we can react to loading and unloading of the composition.
            _player.RegisterPropertyChangedCallback(AnimatedVisualPlayer.IsAnimatedVisualLoadedProperty, (dObj, dProp) => UpdateFileInfo());

            Reset();
        }

        // Called when a composition is loaded or unloaded in the player.
        void UpdateFileInfo()
        {
            var diagnostics = _player.Diagnostics;
            if (diagnostics == null)
            {
                _txtFileName.Text = string.Empty;
                _txtDuration.Text = string.Empty;
                _txtSize.Text = string.Empty;
                PlayerHasIssues = false;
            }
            else
            {
                var diags = (LottieVisualDiagnostics)diagnostics;
                _txtFileName.Text = diags.FileName;
                _txtDuration.Text = $"{diags.Duration.TotalSeconds} secs";
                var aspectRatio = FloatToRatio(diags.LottieWidth / diags.LottieHeight);
                _txtSize.Text = $"{diags.LottieWidth}x{diags.LottieHeight} ({aspectRatio.Item1.ToString("0.##")}:{aspectRatio.Item2.ToString("0.##")})";

                var issues = diags.JsonParsingIssues.Concat(diags.LottieValidationIssues).Concat(diags.TranslationIssues).OrderBy(a => a.Code).ThenBy(a => a.Description).ToArray();
                PlayerIssues = issues;
                PlayerHasIssues = issues.Any();
            }
        }

        internal AnimatedVisualPlayer Player => _player;

        internal bool PlayerHasIssues
        {
            get => (bool)GetValue(PlayerHasIssuesProperty);
            private set => SetValue(PlayerHasIssuesProperty, value);
        }

        internal Issue[] PlayerIssues
        {
            get => (Issue[])GetValue(PlayerIssuesProperty);
            private set => SetValue(PlayerIssuesProperty, value);
        }

        internal LottieVisualSource Source => _playerSource;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public Color ArtboardColor
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get => (Color)GetValue(ArtboardColorProperty);
            set => SetValue(ArtboardColorProperty, value);
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
                await Source.SetSourceAsync(file);
            }
            catch (Exception)
            {
                // Failed to load.
                _player.Opacity = 1;
                UpdateFileInfo();
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
                await Player.PlayAsync(0, 1, true);
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

        // Returns a pleasantly simplified ratio for the given value.
        static (double, double) FloatToRatio(double value)
        {
            const int maxRatioProduct = 200;
            var candidateN = 1.0;
            var candidateD = Math.Round(1 / value);
            var error = Math.Abs(value - (candidateN / candidateD));

            for (double n = candidateN, d = candidateD; n * d <= maxRatioProduct && error != 0;)
            {
                if (value > n / d)
                {
                    n++;
                }
                else
                {
                    d++;
                }

                var newError = Math.Abs(value - (n / d));
                if (newError < error)
                {
                    error = newError;
                    candidateN = n;
                    candidateD = d;
                }
            }

            // If we gave up because the numerator or denominator got too big then
            // the number is an approximation that requires some decimal places.
            // Get the real ratio by adjusting the denominator or numerator - whichever
            // requires the smallest adjustment.
            if (error != 0)
            {
                if (value > candidateN / candidateD)
                {
                    candidateN = candidateD * value;
                }
                else
                {
                    candidateD = candidateN / value;
                }
            }

            return (candidateN, candidateD);
        }
    }
}
