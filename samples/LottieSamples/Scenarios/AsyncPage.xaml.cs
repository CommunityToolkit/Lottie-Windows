// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace LottieSamples.Scenarios
{
    public sealed partial class AsyncPage : Page
    {
        // Describes the segment of the animation to display when hovered.
        private static readonly Segment s_hoveredSegment = new Segment(0, 0.35, false);

        // Describes the segment of the animation to display when clicked.
        private static readonly Segment s_clickedSegment = new Segment(0.35, 1, false);

        private bool _isPlaying;

        public AsyncPage()
        {
            this.InitializeComponent();

            // Start by playing the animation sequence.
            PlayAnimationSequencesAsync();
        }

        private void Players_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // If the animations have completed, start playing again on user interaction.
            if (!_isPlaying)
            {
                PlayAnimationSequencesAsync();
            }
        }

        private async void PlayAnimationSequencesAsync()
        {
            _isPlaying = true;

            // Play the sequence 3 times.
            for (int i = 0; i < 3; i++)
            {
                await PlayAnimationSequenceOnceAsync();
            }

            _isPlaying = false;
        }

        private async Task PlayAnimationSequenceOnceAsync()
        {
            // We await the completion of the PlayAsync method to create 
            // the following order of animation sequences: 
            // 1. P1 Hovered, then
            // 2. P2 Hovered, then
            // 3. P1 Clicked, then
            // 4. P2 Clicked, then
            // 5. P1 Hovered & P2 Hovered together, then
            // 6. P1 Clicked & P2 Clicked together.

            // 1. P1 Hovered.
            await PlaySegmentsAsync(s_hoveredSegment, null);

            // 2. P2 Hovered.
            await PlaySegmentsAsync(null, s_hoveredSegment);

            // 3. P1 Clicked.
            await PlaySegmentsAsync(s_clickedSegment, null);

            // 4. P2 Clicked.
            await PlaySegmentsAsync(null, s_clickedSegment);

            // 5. P1 Hovered & P2 Hovered together.
            await PlaySegmentsAsync(s_hoveredSegment, s_hoveredSegment);

            // 6. P1 Clicked & P2 Clicked together.
            await PlaySegmentsAsync(s_clickedSegment, s_clickedSegment);
        }

        // Plays the given segments on the players.
        private async Task PlaySegmentsAsync(Segment? segmentForPlayerA, Segment? segmentForPlayerB)
        {
            // Draw a highlight around the players that are playing a segment.
            UpdatePlayerHighlights(segmentForPlayerA.HasValue, segmentForPlayerB.HasValue);

            // Start playing the segments.
            var tasks = new[] { segmentForPlayerA?.PlayAsync(PlayerA), segmentForPlayerB?.PlayAsync(PlayerB) };

            // Wait for the segments to finish.
            await Task.WhenAll(tasks.Where(t => t != null).ToArray());

            // Remove the highlight drawn around the playing players.
            UpdatePlayerHighlights(false, false);
        }

        // Updates the highlighting border around each player.
        private void UpdatePlayerHighlights(bool playerAHighlighted, bool playerBHighlighted)
        {
            UpdatePlayerHighlights(PlayerA, playerAHighlighted);
            UpdatePlayerHighlights(PlayerB, playerBHighlighted);
        }

        // Updates the highlighting border around the given player.
        private void UpdatePlayerHighlights(AnimatedVisualPlayer player, bool highlighted)
        {
            var border = player == PlayerA ? PlayerABorder : PlayerBBorder;
            border.BorderBrush = highlighted
                ? (Brush)Resources["SystemControlHighlightAccentBrush"]
                : (Brush)Resources["SystemControlDisabledBaseMediumLowBrush"];
        }

        readonly struct Segment
        {
            public Segment(double fromProgress, double toProgress, bool looping)
                => (FromProgress, ToProgress, Looping) = (fromProgress, toProgress, looping);

            public double FromProgress { get; }

            public double ToProgress { get; }

            public bool Looping { get; }

            // Plays the segment on the given player.
            public async Task PlayAsync(AnimatedVisualPlayer player)
                => await player.PlayAsync(FromProgress, ToProgress, Looping);
        }
    }
}