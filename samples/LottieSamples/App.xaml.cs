﻿using AnimatedVisuals;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace LottieSamples
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                // Ensure the current window is active
                Window.Current.Activate();

                // Run the splash screen animation.
                try
                {
                    await RunAnimatedSplashScreenAsync();
                }
                catch
                {
                    // Ignore any exceptions from the splash screen.
                }

                // Configuring the new page by passing required information as a navigation parameter
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
        }

        // Runs the animated splash screen as content for the current window. The
        // returned Task completes when the animation finishes.
        async Task RunAnimatedSplashScreenAsync()
        {
            // Insert splashGrid above the current window content.
            var originalWindowContent = Window.Current.Content;

            var splashGrid = new Grid();
            splashGrid.Background = (SolidColorBrush)Current.Resources["SystemControlHighlightAccentBrush"];
            Window.Current.Content = splashGrid;

            // Modified LottieLogo1 animation
            var lottieSource = new LottieLogo1_Modified();
            lottieSource.BackgroundColor = (Color)Resources["SystemAccentColor"];
            lottieSource.HighlightColor = (Color)Resources["SystemAccentColorDark2"];

            // Instantiate Player with modified Source
            var player = new AnimatedVisualPlayer
            {
                Stretch = Stretch.Uniform,
                AutoPlay = false,
                Source = lottieSource,
            };

            splashGrid.Children.Add(originalWindowContent);
            splashGrid.Children.Add(player);

            // Start playing the splashscreen animation.
            await player.PlayAsync(fromProgress: 0, toProgress: 0.599, looped: false);

            // Hide the splash screen.
            splashGrid.Visibility = Visibility.Collapsed;
            player.Visibility = Visibility.Collapsed;

            // Restore the original content.
            splashGrid.Children.Clear();
            Window.Current.Content = originalWindowContent;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
