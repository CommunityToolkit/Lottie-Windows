﻿using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Threading.Tasks;

namespace LottieSamples.Scenarios
{
    public sealed partial class AsyncPage : Page
    {
        private static readonly (double fromProgress, double toProgress, bool looping) s_hoveredSegment = (0, 0.35, false);
        private static readonly (double fromProgress, double toProgress, bool looping) s_clickedSegment = (0.35, 1, false);

        public AsyncPage()
        {
            this.InitializeComponent();
            ConfigurePlayOrder();
        }

        private void Players_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // If the animations have completed, start playing again on user interaction.
            if (!PlayerOne.IsPlaying && !PlayerTwo.IsPlaying)
            {
                ConfigurePlayOrder();
            }
        }

        private async void ConfigurePlayOrder()
        {
            // Repeat playing sequences 5 times.
            for (int i=0; i<3; i++)
            {
                await PlayAnimationSequencesAsync();
            }           
        }

        private async Task PlayAnimationSequencesAsync()
        {
            // We await the completion of the PlayAsync method to create 
            // the following order of animation sequences: 
            // 1. P1 Hovered, then
            // 2. P2 Hovered, then
            // 3. P1 Clicked, then
            // 4. P2 Clicked, then
            // 5. P1 Hovered & P2 Hovered together, then
            // 6. P1 Clicked & P2 Clicked together.

            var highlightedBrush = (Windows.UI.Xaml.Media.Brush)Resources["SystemControlHighlightAccentBrush"];
            var disabledBrush = (Windows.UI.Xaml.Media.Brush)Resources["SystemControlDisabledBaseMediumLowBrush"];

            PlayerOneBorder.BorderBrush = highlightedBrush;
            PlayerTwoBorder.BorderBrush = disabledBrush;
            await PlayerOne.PlayAsync(s_hoveredSegment.fromProgress, s_hoveredSegment.toProgress, s_hoveredSegment.looping);

            PlayerOneBorder.BorderBrush = disabledBrush;
            PlayerTwoBorder.BorderBrush = highlightedBrush;
            await PlayerTwo.PlayAsync(s_hoveredSegment.fromProgress, s_hoveredSegment.toProgress, s_hoveredSegment.looping);

            PlayerOneBorder.BorderBrush = highlightedBrush;
            PlayerTwoBorder.BorderBrush = disabledBrush;
            await PlayerOne.PlayAsync(s_clickedSegment.fromProgress, s_clickedSegment.toProgress, s_clickedSegment.looping);

            PlayerOneBorder.BorderBrush = disabledBrush;
            PlayerTwoBorder.BorderBrush = highlightedBrush;
            await PlayerTwo.PlayAsync(s_clickedSegment.fromProgress, s_clickedSegment.toProgress, s_clickedSegment.looping);

            PlayerOneBorder.BorderBrush = highlightedBrush;
            PlayerTwoBorder.BorderBrush = highlightedBrush;
            await Task.WhenAll(PlayerOne.PlayAsync(s_hoveredSegment.fromProgress, s_hoveredSegment.toProgress, s_hoveredSegment.looping).AsTask(),
                               PlayerTwo.PlayAsync(s_hoveredSegment.fromProgress, s_hoveredSegment.toProgress, s_hoveredSegment.looping).AsTask());

            PlayerOneBorder.BorderBrush = highlightedBrush;
            PlayerTwoBorder.BorderBrush = highlightedBrush;
            await Task.WhenAll(PlayerOne.PlayAsync(s_clickedSegment.fromProgress, s_clickedSegment.toProgress, s_clickedSegment.looping).AsTask(),
                               PlayerTwo.PlayAsync(s_clickedSegment.fromProgress, s_clickedSegment.toProgress, s_clickedSegment.looping).AsTask());

            PlayerOneBorder.BorderBrush = disabledBrush;
            PlayerTwoBorder.BorderBrush = disabledBrush;
        }      
    }
}