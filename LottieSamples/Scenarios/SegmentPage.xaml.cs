// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace LottieSamples.Scenarios
{
    public sealed partial class SegmentPage : Page
    {
        private static readonly (double fromProgress, double toProgress, bool looping) s_hoveredSegment = (0, 0.35, true);
        private static readonly (double fromProgress, double toProgress, bool looping) s_clickedSegment = (0.35, 1, false);
        private bool _stateOn;     
        
        public SegmentPage()
        {
            this.InitializeComponent();
        }

        private void Segments_Player_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (Segments_Player.IsPlaying)
            {
                // Must be playing on click: do nothing.
            }
            else
            {
                if (!_stateOn)
                {
                    // Play "Hovered" segment of the animation.
                    _ = Segments_Player.PlayAsync(s_hoveredSegment.fromProgress, s_hoveredSegment.toProgress, s_hoveredSegment.looping);
                }
            }
        }

        private void Segments_Player_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (Segments_Player.IsPlaying && !_stateOn)
            {
                // Stop playing "Hovered" segment, which also resets the animation to its initial frame.
                Segments_Player.Stop();
            }
        }

        private void Segments_Player_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_stateOn)
            {
                // Reset to Off state if already On.
                _stateOn = false;                
                Segments_Player.SetProgress(0);
            }
            else
            {
                // Play "Clicked" segment of the animation.
                _stateOn = true;
                _ = Segments_Player.PlayAsync(s_clickedSegment.fromProgress, s_clickedSegment.toProgress, s_clickedSegment.looping);
            }
        }
    }
}