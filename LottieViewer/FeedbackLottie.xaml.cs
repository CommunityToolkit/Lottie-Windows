// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace LottieViewer
{
    /// <summary>
    /// FeedbackLottie.
    /// </summary>
    public sealed partial class FeedbackLottie : UserControl
    {
        // Shrinks from the expanded JSON file back to the initial size.
        static readonly CompositionSegment ShrinkToInitial =
            new CompositionSegment("ShrinkToInitial", 0.007, 0.1188811, playbackRate: -1, isLoopingEnabled: false);

        // Expands from the initial state to a large JSON file.
        static readonly CompositionSegment ExpandFromInitial = new CompositionSegment("ExpandFromInitial", 0.007, 0.1188811);

        // A loop where the JSON file looks excited about being dropped.
        static readonly CompositionSegment ExcitedDropLoop =
            new CompositionSegment("ExcitedDropLoop", 0.1188811, 0.3426574, playbackRate: 1, isLoopingEnabled: true);

        // Follows on from ExcitedDropLoop.
        static readonly CompositionSegment ExcitedResolution = new CompositionSegment("ExcitedResolution", 0.3426574, 0.489);

        // The explosion at the end of loading.
        static readonly CompositionSegment FinishLoading = new CompositionSegment("FinishLoading", 0.6923078, 1);

        static readonly CompositionSegment FailLoading = new CompositionSegment("FailLoading", 0.4895105, 0.69 /* 0.6923077 */);

        Task _currentPlay = Task.CompletedTask;
        DragNDropHintState _dragNDropHintState = DragNDropHintState.Initial;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public FeedbackLottie()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            this.InitializeComponent();
        }

        internal void PlayInitialStateAnimation()
        {
            switch (_dragNDropHintState)
            {
                case DragNDropHintState.Failed:
                case DragNDropHintState.Finished:
                case DragNDropHintState.Initial:
                    break;

                case DragNDropHintState.Disabled:
                case DragNDropHintState.Encouraging:
                case DragNDropHintState.Shrinking:
                default:
                    return;
            }

            EnsureVisible();
            _dragNDropHintState = DragNDropHintState.Initial;
            _dragNDropHint.SetProgress(0.007);
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100
        internal async void PlayDragEnterAnimation()
        {
#pragma warning restore VSTHRD100
            EnsureVisible();
            if (_dragNDropHintState == DragNDropHintState.Initial ||
                _dragNDropHintState == DragNDropHintState.Shrinking)
            {
                _dragNDropHintState = DragNDropHintState.Encouraging;
                try
                {
                    await PlaySegmentAsync(ExpandFromInitial);
                    await PlaySegmentAsync(ExcitedDropLoop);
                }
                catch
                {
                    // Ignore async exceptions so they won't crash the process.
                }
            }
        }

        internal async Task PlayDroppedAnimationAsync()
        {
            EnsureVisible();
            if (_dragNDropHintState == DragNDropHintState.Encouraging)
            {
                await PlaySegmentAsync(ExcitedResolution);
                if (_dragNDropHintState != DragNDropHintState.Encouraging)
                {
                    return;
                }
            }

            _dragNDropHintState = DragNDropHintState.Finished;

            // Fade out. This is only necessary for RS4 builds that
            // do not handle 0-size strokes correctly, leaving crud on
            // the screen.
            _fadeOutStoryboard.Begin();

            await PlaySegmentAsync(FinishLoading);
        }

        internal void PlayDragLeaveAnimation()
        {
            EnsureVisible();
            if (_dragNDropHintState == DragNDropHintState.Encouraging)
            {
                _dragNDropHintState = DragNDropHintState.Shrinking;
#pragma warning disable VSTHRD110 // Observe result of async calls
                PlaySegmentAsync(ShrinkToInitial);
#pragma warning restore VSTHRD110 // Observe result of async calls
            }
        }

        internal async Task PlayLoadFailedAnimationAsync()
        {
            EnsureVisible();
            _dragNDropHintState = DragNDropHintState.Failed;
            await PlaySegmentAsync(FailLoading);
            _dragNDropHintState = DragNDropHintState.Initial;
            _dragNDropHint.SetProgress(FailLoading.ToProgress);
        }

        Task PlaySegmentAsync(CompositionSegment segment)
        {
            _dragNDropHint.PlaybackRate = segment.PlaybackRate;

            return _currentPlay = _dragNDropHint.PlayAsync(
                fromProgress: segment.FromProgress,
                toProgress: segment.ToProgress,
                looped: segment.IsLoopingEnabled).AsTask();
        }

        void EnsureVisible()
        {
            Debug.WriteLine("Stopping opacity animation");
            _fadeOutStoryboard.Stop();
            _dragNDropHint.Opacity = 1;
        }

        enum DragNDropHintState
        {
            Disabled,
            Initial,
            Encouraging,
            Finished,
            Failed,
            Shrinking,
        }

        /// <summary>
        /// Defines a segment of a composition that can be played by the AnimatedVisualPlayer.
        /// </summary>
        sealed class CompositionSegment
        {
            public double FromProgress { get; }

            public double ToProgress { get; }

            public double PlaybackRate { get; }

            public bool IsLoopingEnabled { get; }

            public string Name { get; }

            public CompositionSegment(string name, double fromProgress, double toProgress, double playbackRate, bool isLoopingEnabled)
            {
                Name = name;
                FromProgress = fromProgress;
                ToProgress = toProgress;
                PlaybackRate = playbackRate;
                IsLoopingEnabled = isLoopingEnabled;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="CompositionSegment"/> class.
            /// Defines a segment that plays from <paramref name="fromProgress"/> to <paramref name="toProgress"/>
            /// without looping or repeating.
            /// </summary>
            public CompositionSegment(string name, double fromProgress, double toProgress)
                : this(name, fromProgress, toProgress, playbackRate: 1, isLoopingEnabled: false)
            {
            }
        }
    }
}
