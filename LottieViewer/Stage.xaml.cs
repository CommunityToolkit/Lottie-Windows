// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Threading.Tasks;
using LottieViewer.ViewModel;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;

namespace LottieViewer
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// This is where the Lottie file is displayed. This is a wrapper around the
    /// AnimatedVisualPlayer that plays a loading animation and exposes the
    /// diagnostics object as a view model.
    /// </summary>
    public sealed partial class Stage : UserControl
    {
        // The color of the artboard is a dependency property so that it can be the
        // target of binding.
        public static readonly DependencyProperty ArtboardColorProperty =
            DependencyProperty.Register("ArtboardColor", typeof(Color), typeof(Stage), new PropertyMetadata(Colors.Black));

        public Stage()
        {
            this.InitializeComponent();

            Reset();

            _ = SetupBackgroundPatternAsync();

            ShowSolidBackground = false;
        }

        // Draw repeating paatern texture on backgound canvas.
        public async Task SetupBackgroundPatternAsync()
        {
            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var canvasDevice = CanvasDevice.GetSharedDevice();
            var graphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, canvasDevice);

            var bitmap = await CanvasBitmap.LoadAsync(canvasDevice, @"Assets\BackgroundPattern.png");

            var drawingSurface = graphicsDevice.CreateDrawingSurface(
                bitmap.Size,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);

            using (var ds = CanvasComposition.CreateDrawingSession(drawingSurface))
            {
                ds.Clear(Colors.Transparent);
                ds.DrawImage(bitmap);
            }

            var surfaceBrush = compositor.CreateSurfaceBrush(drawingSurface);
            surfaceBrush.Stretch = CompositionStretch.None;

            var border = new BorderEffect
            {
                ExtendX = CanvasEdgeBehavior.Wrap,
                ExtendY = CanvasEdgeBehavior.Wrap,
                Source = new CompositionEffectSourceParameter("source"),
            };

            var fxFactory = compositor.CreateEffectFactory(border);
            var fxBrush = fxFactory.CreateBrush();
            fxBrush.SetSourceParameter("source", surfaceBrush);

            var sprite = compositor.CreateSpriteVisual();
            sprite.Size = new Vector2(4096);
            sprite.Brush = fxBrush;

            ElementCompositionPreview.SetElementChildVisual(_canvas, sprite);
        }

        // The DiagnosticsViewModel contains information about the currently playing
        // Lottie file. This information is consumed by other controls such as the
        // color picker and scrubber.
        internal LottieVisualDiagnosticsViewModel DiagnosticsViewModel => _diagnosticsViewModel;

        internal AnimatedVisualPlayer Player => _player;

        internal Viewbox PlayerContainer => _playerContainer;

        private bool showSolidBackground = false;

        internal bool ShowSolidBackground
        {
            get => showSolidBackground;

            set
            {
                _backgroundColorBrush.Opacity = value ? 1.0 : 0.0;
                showSolidBackground = value;
            }
        }

        public Color ArtboardColor
        {
            get { return (Color)GetValue(ArtboardColorProperty); }
            set { SetValue(ArtboardColorProperty, value); }
        }

        internal async Task<bool> TryLoadFileAsync(StorageFile file)
        {
            var startDroppedAnimation = _feedbackLottie.PlayDroppedAnimationAsync();

            _player.Opacity = 0;
            try
            {
                // Load the Lottie composition.
                await _playerSource.SetSourceAsync(file);
                _canvas.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                // Failed to load.
                _player.Opacity = 1;
                try
                {
                    await _feedbackLottie.PlayLoadFailedAnimationAsync();
                }
                catch
                {
                    // Ignore PlayLoadFailedAnimationAsync exceptions so they don't crash the process.
                }

                return false;
            }

            // Wait until the dropping animation has finished.
            await startDroppedAnimation;

            _player.Opacity = 1;
            return true;
        }

        internal void DoDragEnter()
        {
            _feedbackLottie.PlayDragEnterAnimation();
        }

        internal void DoDragLeave()
        {
            _feedbackLottie.PlayDragLeaveAnimation();
        }

        internal void Reset()
        {
            _canvas.Visibility = Visibility.Collapsed;
            _feedbackLottie.PlayInitialStateAnimation();
        }
    }
}
