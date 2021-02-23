// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading.Tasks;
using AnimatedVisuals;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace LottieViewer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        // Avoid "async void" method. Not valid here because we handle all async exceptions.
#pragma warning disable VSTHRD100
        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
#pragma warning restore VSTHRD100
            var rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame is null)
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

            if (!e.PrelaunchActivated)
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

                // Start navigation to the first page.
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected override void OnWindowCreated(WindowCreatedEventArgs args)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            if (titleBar is not null)
            {
                var backgroundColor = (SolidColorBrush)Current.Resources["BackgroundBrush"];
                var foregroundColor = (SolidColorBrush)Current.Resources["ForegroundBrush"];

                titleBar.ButtonBackgroundColor = backgroundColor.Color;
                titleBar.ButtonForegroundColor = foregroundColor.Color;
                titleBar.BackgroundColor = backgroundColor.Color;
                titleBar.ForegroundColor = foregroundColor.Color;

                titleBar.InactiveBackgroundColor = backgroundColor.Color;
                titleBar.InactiveForegroundColor = foregroundColor.Color;

                titleBar.ButtonInactiveBackgroundColor = backgroundColor.Color;
                titleBar.ButtonInactiveForegroundColor = foregroundColor.Color;
            }

            base.OnWindowCreated(args);
        }

        // Runs the animated splash screen as content for the current window. The
        // returned Task completes when the animation finishes.
        async Task RunAnimatedSplashScreenAsync()
        {
            // Insert splashBorder above the current window content.
            var originalWindowContent = Window.Current.Content;
            var splashBorder = new Border
            {
                Background = (SolidColorBrush)Current.Resources["LottieBasicBrush"],
            };

            var player = new AnimatedVisualPlayer
            {
                Stretch = Stretch.Uniform,
                AutoPlay = false,
                Source = new LottieLogo(),
            };

            splashBorder.Child = player;

            Window.Current.Content = splashBorder;

            // Start playing.
            await player.PlayAsync(fromProgress: 0, toProgress: 0.599, looped: false);

            // Reset window content after the splashscreen animation has completed.
            Window.Current.Content = originalWindowContent;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails.
        /// </summary>
        /// <param name="sender">The Frame which failed navigation.</param>
        /// <param name="e">Details about the navigation failure.</param>
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