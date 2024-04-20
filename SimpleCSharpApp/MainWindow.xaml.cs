using CommunityToolkit.WinAppSDK.LottieIsland;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace SimpleCSharpApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private LottieWinRT.LottieVisualSourceWinRT? _lottieVisualSource;
        private LottieContentIsland? _lottieContentIsland;

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void MyButton_Click(object sender, RoutedEventArgs e)
        {
            myButton.Content = "Clicked";
            _lottieVisualSource = LottieWinRT.LottieVisualSourceWinRT.CreateFromString("ms-appx:///Assets/LottieLogo1.json");
            if (_lottieVisualSource != null)
            {
                _lottieVisualSource.AnimatedVisualInvalidated += LottieVisualSource_AnimatedVisualInvalidated;
            }
            else
            {
                Debug.WriteLine("Failed to load LottieVisualSourceWinRT from file");
            }

            _lottieContentIsland = LottieContentIsland.Create(this.Compositor);
            if (_lottieContentIsland != null)
            {
                Debug.WriteLine("LottieContentIsland created!");
            }
            else
            {
                Debug.WriteLine("LottieContentIsland creation failed :(");
            }
        }

        private void LottieVisualSource_AnimatedVisualInvalidated(object? sender, object? e)
        {
            Debug.Assert(_lottieVisualSource != null);
            {
                object? diagnostics = null;
                IAnimatedVisualFrameworkless? animatedVisual = _lottieVisualSource.TryCreateAnimatedVisual(this.Compositor, out diagnostics);
                if (animatedVisual != null)
                {
                    Debug.WriteLine("Lottie duration: " + animatedVisual.Duration);
                }
                else
                {
                    Debug.WriteLine("Visual creation failed");
                }
            }
        }
    }
}
