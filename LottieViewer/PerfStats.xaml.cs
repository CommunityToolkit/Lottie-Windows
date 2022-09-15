using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using LottieViewer.ViewModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace LottieViewer
{
    /// <summary>
    /// Displays perf stats.
    /// </summary>
    internal sealed partial class PerfStats : UserControl
    {
        public PerfStats()
        {
            this.InitializeComponent();
        }

        public ObservableCollection<object> InputLayers { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> InputMasks { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> InputEffects { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> OutputTotal { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> OutputVisuals { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> OutputAnimations { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> OutputEffects { get; } = new ObservableCollection<object>();

        public void Update(LottieVisualDiagnosticsViewModel diagnostics)
        {
            InputLayers.Clear();
            InputMasks.Clear();
            InputEffects.Clear();
            OutputTotal.Clear();
            OutputVisuals.Clear();
            OutputAnimations.Clear();
            OutputEffects.Clear();

            foreach (var statsEntry in diagnostics.Stats)
            {
                if ((statsEntry.Tags & ~(StatsEntry.LOTTIE_COMPOSITION_TAG | StatsEntry.LAYER_TAG)) == 0)
                {
                    InputLayers.Add(statsEntry);
                }

                if ((statsEntry.Tags & ~(StatsEntry.LOTTIE_COMPOSITION_TAG | StatsEntry.MASK_TAG)) == 0)
                {
                    InputMasks.Add(statsEntry);
                }

                if ((statsEntry.Tags & ~(StatsEntry.LOTTIE_COMPOSITION_TAG | StatsEntry.EFFECT_TAG)) == 0)
                {
                    InputEffects.Add(statsEntry);
                }

                if ((statsEntry.Tags & ~StatsEntry.WINDOWS_COMPOSITION_TAG) == 0)
                {
                    OutputTotal.Add(statsEntry);
                }
            }

            if (diagnostics.LottieVisualDiagnostics is not null && diagnostics.LottieVisualDiagnostics.WinCompStats is not null)
            {
                ScoreValue.Text = string.Format("{0:0.00}", diagnostics.LottieVisualDiagnostics!.WinCompStats!.CalculateApproximateComplexity());
                AnimationsComplexity.Text = string.Format("{0:0.00}", diagnostics.LottieVisualDiagnostics!.WinCompStats!.CalculateApproximateAnimationsComplexity());
                GeometryComplexity.Text = string.Format("{0:0.00}", diagnostics.LottieVisualDiagnostics!.WinCompStats!.CalculateApproximateGeometryComplexity());
                EffectsComplexity.Text = string.Format("{0:0.00}", diagnostics.LottieVisualDiagnostics!.WinCompStats!.CalculateApproximateEffectsComplexity());
                TreeComplexity.Text = string.Format("{0:0.00}", diagnostics.LottieVisualDiagnostics!.WinCompStats!.CalculateApproximateTreeComplexity());
            }
        }
    }
}
