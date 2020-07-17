// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie;

namespace LottieViewer
{
    /// <summary>
    /// View model class for <see cref="LottieVisualDiagnostics"/>.
    /// </summary>
    sealed class LottieVisualDiagnosticsViewModel : INotifyPropertyChanged
    {
        LottieVisualDiagnostics _wrapped;

        public event PropertyChangedEventHandler PropertyChanged;

        public object DiagnosticsObject
        {
            get => _wrapped;

            set
            {
                _wrapped = (LottieVisualDiagnostics)value;
                PlayerIssues.Clear();
                Markers.Clear();
                if (value != null)
                {
                    foreach (var issue in _wrapped.JsonParsingIssues.
                                            Concat(_wrapped.LottieValidationIssues).
                                            Concat(_wrapped.TranslationIssues).
                                            OrderBy(a => a.Code).
                                            ThenBy(a => a.Description))
                    {
                        PlayerIssues.Add(issue);
                    }

                    foreach (var marker in _wrapped.Markers)
                    {
                        Markers.Add((marker.Key, marker.Value));
                    }
                }

                var propertyChangedCallback = PropertyChanged;
                if (propertyChangedCallback != null)
                {
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(DurationText)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(FileName)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(MarkersText)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(PlayerHasIssues)));
                    propertyChangedCallback(this, new PropertyChangedEventArgs(nameof(SizeText)));
                }
            }
        }

        public string DurationText => _wrapped is null ? string.Empty : $"{_wrapped.Duration.TotalSeconds} secs";

        public string FileName => _wrapped?.FileName ?? string.Empty;

        public ObservableCollection<(string Name, double Offset)> Markers { get; } = new ObservableCollection<(string, double)>();

        public string MarkersText =>
            _wrapped is null ? string.Empty : string.Join(", ", Markers.Select(value => $"{value.Name}={value.Offset:0.###}"));

        public bool PlayerHasIssues => PlayerIssues.Count > 0;

        public ObservableCollection<Issue> PlayerIssues { get; } = new ObservableCollection<Issue>();

        public string SizeText
        {
            get
            {
                if (_wrapped is null)
                {
                    return string.Empty;
                }

                var aspectRatio = FloatToRatio(_wrapped.LottieWidth / _wrapped.LottieHeight);
                return $"{_wrapped.LottieWidth}x{_wrapped.LottieHeight} ({aspectRatio.Item1:0.##}:{aspectRatio.Item2:0.##})";
            }
        }

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
