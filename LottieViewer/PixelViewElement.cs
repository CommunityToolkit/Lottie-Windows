// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace LottieViewer
{
    /// <summary>
    /// PixelView element can draw any provided visual and scale it up so that
    /// pixels are easily visible.
    /// This element also provides information about pixel under cursor: position, color.
    /// </summary>
    public sealed class PixelViewElement : UserControl
    {
        readonly SpriteVisual _spriteVisual;
        Visual? _capturedVisual = null;

        // Checkerboard pattern bitmap.
        static CanvasBitmap? _patternBitmap = null;

        // Previously rendered bitmap info.
        CanvasBitmap? _currentBitmap = null;
        Color[]? _currentColors = null;

        Color? _currentColor = null;

        GraphicsCaptureSession? _session = null;
        Direct3D11CaptureFramePool _framePool;
        ICompositionSurface _surface;
        CanvasSwapChain _swapchain;
        CanvasDevice _canvasDevice;

        public static readonly DependencyProperty CurrentColorStringProperty =
        DependencyProperty.RegisterAttached(
          "CurrentColorString",
          typeof(string),
          typeof(PixelViewElement),
          new PropertyMetadata("#FFFFFFFF")
        );

        public static readonly DependencyProperty CurrentPositionProperty =
        DependencyProperty.RegisterAttached(
          "CurrentColorString",
          typeof(Vector2),
          typeof(PixelViewElement),
          new PropertyMetadata(new Vector2(0, 0))
        );

        // Contains hex representation of last color under cursor #AARRGGBB
        public string CurrentColorString
        {
            get { return (string)GetValue(CurrentColorStringProperty); }
            set { SetValue(CurrentColorStringProperty, value); }
        }

        // 2d position of last pixel under cursor <x, y>
        public Vector2 CurrentPosition
        {
            get { return (Vector2)GetValue(CurrentPositionProperty); }
            set { SetValue(CurrentPositionProperty, value); }
        }

        public PixelViewElement()
        {
            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _spriteVisual = compositor.CreateSpriteVisual();
            _spriteVisual.RelativeSizeAdjustment = Vector2.One;
            ElementCompositionPreview.SetElementChildVisual(this, _spriteVisual);

            _canvasDevice = CanvasDevice.GetSharedDevice();

            // Swapchain will be resized later
            _swapchain = new CanvasSwapChain(_canvasDevice, 512, 512, 96);
            _surface = CanvasComposition.CreateCompositionSurfaceForSwapChain(compositor, _swapchain);
            _spriteVisual.Brush = compositor.CreateSurfaceBrush(_surface);

            // Create a frame pool with room for only 1 frame because we're getting a single frame, not a video.
            _framePool =
                Direct3D11CaptureFramePool.Create(_canvasDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, new SizeInt32 { Width = 512, Height = 512 });

            _framePool.FrameArrived += OnFrameArrived;
        }

        public void SetElementToCapture(FrameworkElement element)
        {
            element.SizeChanged += (object sender, SizeChangedEventArgs e) =>
                  OnResolutionUpdated((int)element.ActualWidth, (int)element.ActualHeight);

            _capturedVisual = ElementCompositionPreview.GetElementVisual(element);
            _capturedVisual.BorderMode = CompositionBorderMode.Soft;

            OnResolutionUpdated((int)element.ActualWidth, (int)element.ActualHeight);

            _session = _framePool.CreateCaptureSession(GraphicsCaptureItem.CreateFromVisual(_capturedVisual));
            _session.StartCapture();
        }

        public void OnMouseMove(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(this).Position;

            if (_currentBitmap == null || ActualHeight <= 0)
            {
                return;
            }

            int pixelX = (int)Math.Floor(position.X * (_currentBitmap.Size.Height / ActualHeight));
            int pixelY = (int)Math.Floor(position.Y * (_currentBitmap.Size.Width / ActualWidth));

            if (pixelX < 0 || pixelX >= _currentBitmap.SizeInPixels.Width || pixelY < 0 || pixelY >= _currentBitmap.SizeInPixels.Height)
            {
                return;
            }

            if (_currentColors == null)
            {
                _currentColors = _currentBitmap.GetPixelColors();
            }

            _currentColor = _currentColors[(pixelY * _currentBitmap.SizeInPixels.Width) + pixelX];
            SetValue(CurrentColorStringProperty, _currentColor.ToString());
            SetValue(CurrentPositionProperty, new Vector2(pixelX, pixelY));
        }

        async Task ShowBitmapOnTargetAsync(CanvasBitmap bitmap)
        {
            int scale = Math.Max((int)Math.Ceiling(ActualSize.Y / bitmap.Size.Height), 1);

            Height = bitmap.Size.Height / bitmap.Size.Width * ActualSize.X;

            _currentBitmap?.Dispose();
            _currentBitmap = bitmap;
            _currentColors = null;

            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            if (_patternBitmap is null)
            {
                _patternBitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), @"Assets\BackgroundPattern.png");
            }

            var size = new Size(bitmap.Size.Width * scale, bitmap.Size.Height * scale);

            if (_swapchain.Size.Width != size.Width || _swapchain.Size.Height != size.Height)
            {
                _swapchain.ResizeBuffers(size);
            }

            using (var drawingSession = _swapchain!.CreateDrawingSession(Colors.Transparent))
            {
                var border = new BorderEffect
                {
                    ExtendX = CanvasEdgeBehavior.Wrap,
                    ExtendY = CanvasEdgeBehavior.Wrap,
                    Source = _patternBitmap,
                };

                var scaleEffect = new ScaleEffect
                {
                    Scale = new Vector2(scale, scale),
                    InterpolationMode = CanvasImageInterpolation.NearestNeighbor,
                };

                scaleEffect.Source = border;
                drawingSession.DrawImage(scaleEffect);
                scaleEffect.Source = bitmap;
                drawingSession.DrawImage(scaleEffect);
            }

            _swapchain.Present();
        }

        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using (var frame = sender.TryGetNextFrame())
            {
                CanvasBitmap bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice!, frame.Surface);
                _ = ShowBitmapOnTargetAsync(bitmap);
            }
        }

        public void OnResolutionUpdated(int width, int height)
            => UpdateResolution(new SizeInt32 { Width = width, Height = height });

        public void UpdateResolution(SizeInt32 size)
        {
            if (size.Width == 0 || size.Height == 0)
            {
                return;
            }

            _framePool.Recreate(_canvasDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, new SizeInt32 { Width = size.Width, Height = size.Height });
        }
    }
}
