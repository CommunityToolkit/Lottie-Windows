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
        internal static (double, double) FloatToRatio(double value)
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
