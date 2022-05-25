// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.WinUI.Lottie;
using CommunityToolkit.WinUI.Lottie.CompMetadata;
using CommunityToolkit.WinUI.Lottie.LottieMetadata;
using Windows.Media.Audio;

namespace LottieViewer.ViewModel
{
    /// <summary>
    /// View model class for <see cref="LottieVisualDiagnostics"/>.
    /// </summary>
    sealed class LottieVisualDiagnosticsViewModel : INotifyPropertyChanged
    {
        // How much past the start of the frame we consider to be the actual start of the
        // frame. Nudging is done to compensate for frames being integers, but progress
        // is floating point, which can cause math done to the progress value to round
        // down to the previous frame value.
        public const double NudgeFrameProportion = 0.05;

        public event PropertyChangedEventHandler? PropertyChanged;

        public object? DiagnosticsObject
        {
            get => LottieVisualDiagnostics;

            set
            {
                LottieVisualDiagnostics = (LottieVisualDiagnostics?)value;
                Issues.Clear();
                Markers.Clear();
                ThemePropertyBindings.Clear();
                ThemingPropertySet = null;

                if (LottieVisualDiagnostics is not null)
                {
                    // Populate the issues list.
                    foreach (var issue in LottieVisualDiagnostics.JsonParsingIssues.
                                            Concat(LottieVisualDiagnostics.LottieValidationIssues).
                                            Concat(LottieVisualDiagnostics.TranslationIssues).
                                            OrderBy(a => a.Code).
                                            ThenBy(a => a.Description))
                    {
                        Issues.Add(issue);
                    }

                    // Populate the marker info.
                    var composition = LottieVisualDiagnostics.LottieComposition;
                    if (composition is not null)
                    {
                        var metadata = new LottieCompositionMetadata(
                            composition.Name,
                            composition.FramesPerSecond,
                            composition.InPoint,
                            composition.OutPoint,
                            composition.Markers.Select(m => (m.Name, m.Frame, m.DurationInFrames)));

                        var framesPerSecond = composition.FramesPerSecond;
                        var duration = composition.Duration.TotalSeconds;
                        var totalFrames = framesPerSecond * duration;

                        var isFirst = true;
                        foreach (var m in metadata.FilteredMarkers)
                        {
                            var inProgress = m.Frame.GetNudgedProgress(NudgeFrameProportion);
                            Marker marker;
                            var propertyName = isFirst ? $"Marker{(composition.Markers.Count > 1 ? "s" : string.Empty)}" : string.Empty;

                            if (m.Duration.Frames == 0)
                            {
                                marker = new Marker(m.Name, propertyName, (int)m.Frame.Number, inProgress);
                            }
                            else
                            {
                                var outProgress = (m.Frame + m.Duration).GetNudgedProgress(NudgeFrameProportion);
                                marker = new MarkerWithDuration(
                                    m.Name,
                                    propertyName,
                                    (int)m.Frame.Number,
                                    inProgress,
                                    (int)(m.Frame + m.Duration).Number,
                                    outProgress);
                            }

                            isFirst = false;
                            Markers.Add(marker);
                        }
                    }

                    ThemingPropertySet = LottieVisualDiagnostics.ThemingPropertySet;
                    if (LottieVisualDiagnostics.ThemePropertyBindings is not null)
                    {
                        foreach (var binding in LottieVisualDiagnostics.ThemePropertyBindings)
                        {
                            ThemePropertyBindings.Add(binding);
                        }
                    }
                }

                var propertyChangedCallback = PropertyChanged;
                if (propertyChangedCallback is not null)
                {
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(DurationText)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(FileName)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(HasIssues)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(LottieVisualDiagnostics)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(Name)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(SizeText)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(ThemingPropertySet)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(DiagnosticsObject)));
                }
            }
        }

        internal LottieVisualDiagnostics? LottieVisualDiagnostics { get; private set; }

        public string DurationText
        {
            get
            {
                if (LottieVisualDiagnostics is null)
                {
                    return string.Empty;
                }
                else
                {
                    var seconds = LottieVisualDiagnostics.Duration.TotalSeconds;
                    return $"{seconds:0.##} second{(seconds == 1 ? string.Empty : "s")}";
                }
            }
        }

        public Duration Duration => new Duration(FrameCount, FramesPerSecond);

        public double FramesPerSecond => LottieVisualDiagnostics?.LottieComposition?.FramesPerSecond ?? 0;

        public double FrameCount => LottieVisualDiagnostics?.LottieComposition?.FrameCount ?? 0;

        public string FrameCountText => FrameCount.ToString();

        public string Name => LottieVisualDiagnostics?.LottieComposition?.Name ?? string.Empty;

        public string FileName => LottieVisualDiagnostics?.FileName ?? string.Empty;

        public ObservableCollection<Marker> Markers { get; } = new ObservableCollection<Marker>();

        public ObservableCollection<PropertyBinding> ThemePropertyBindings { get; } = new ObservableCollection<PropertyBinding>();

        public Windows.UI.Composition.CompositionPropertySet? ThemingPropertySet { get; private set; }

        public bool HasIssues => Issues.Count > 0;

        public ObservableCollection<Issue> Issues { get; } = new ObservableCollection<Issue>();

        public string SizeText
        {
            get
            {
                if (LottieVisualDiagnostics is null)
                {
                    return string.Empty;
                }

                var aspectRatio = FloatToRatio(LottieVisualDiagnostics.LottieWidth / LottieVisualDiagnostics.LottieHeight);
                return $"{LottieVisualDiagnostics.LottieWidth}x{LottieVisualDiagnostics.LottieHeight} ({aspectRatio.Item1:0.##}:{aspectRatio.Item2:0.##})";
            }
        }

        // Get the frame with the given number.
        public Frame GetFrameFromFrameNumber(double frameNumber)
            => LottieVisualDiagnostics is null ? default : new Frame(Duration, frameNumber);

        // Returns the nudged progress that is equivalent to the given frame number.
        public double GetNudgedProgressFromFrame(double frameNumber)
            => LottieVisualDiagnostics is null ? default : GetFrameFromFrameNumber(frameNumber).GetNudgedProgress(NudgeFrameProportion);

        // Converts the given nudged progress value to a frame number.
        public Frame GetFrameFromNudgedProgress(double nudgedProgress)
            => LottieVisualDiagnostics is null ? default : Duration.GetFrameFromNudgedProgress(nudgedProgress, NudgeFrameProportion);

        // Returns a pleasantly simplified ratio for the given value.
        // For example an aspect ratio of 800 x 600 will result in a call to here
        // as value = 800/600 = 1.333, and will be simplified to 4 x 3.
        internal static (double numerator, double denominator) FloatToRatio(double value)
        {
            // This value determines how large we will let the numerator or denominator get.
            // If we didn't set a maximum, non-rational values would end up with an infinite
            // numerator or denominator.
            const int maxRatioProduct = 200;

            // Start with an estimate of the numerator and denominator. We will iterate to
            // improve the estimate until we either get it exactly right or the numerator and
            // denominator are so big that we'll give up trying to express the result as
            // an integer ratio and will return an integer and a floating point value.
            var candidateN = 1.0;

            // NOTE: if value is 0, candidateD will be infinity. This is not a problem - we'll
            //       end up returning 1 x infinity, which is the correct result.
            var candidateD = Math.Round(1 / value);

            // See how close our estimate is.
            var error = Math.Abs(value - (candidateN / candidateD));

            // Iterate until we get sufficiently close.
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
