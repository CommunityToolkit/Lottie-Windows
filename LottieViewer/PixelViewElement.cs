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

#nullable enable

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
        CanvasBitmap? _previousBitmap = null;
        Color[]? _previousColors = null;

        Color? _currentColor = null;

        Direct3D11CaptureFramePool? _framePool = null;
        GraphicsCaptureSession? _session = null;
        CanvasDevice? _canvasDevice = null;

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
        }

        public void SetElementToCapture(FrameworkElement element)
        {
            element.SizeChanged += (object sender, SizeChangedEventArgs e) =>
                  OnResolutionUpdated((int)element.ActualWidth, (int)element.ActualHeight);

            _capturedVisual = ElementCompositionPreview.GetElementVisual(element);
            _capturedVisual.BorderMode = CompositionBorderMode.Soft;

            OnResolutionUpdated((int)element.ActualWidth, (int)element.ActualHeight);
        }

        public void OnMouseMove(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(this).Position;

            if (_previousBitmap == null || ActualHeight <= 0)
            {
                return;
            }

            int pixelX = (int)Math.Floor(position.X * (_previousBitmap.Size.Height / ActualHeight));
            int pixelY = (int)Math.Floor(position.Y * (_previousBitmap.Size.Width / ActualWidth));

            if (pixelX < 0 || pixelX >= _previousBitmap.SizeInPixels.Width || pixelY < 0 || pixelY >= _previousBitmap.SizeInPixels.Height)
            {
                return;
            }

            if (_previousColors == null)
            {
                _previousColors = _previousBitmap.GetPixelColors();
            }

            _currentColor = _previousColors[(pixelY * _previousBitmap.SizeInPixels.Width) + pixelX];
            SetValue(CurrentColorStringProperty, _currentColor.ToString());
            SetValue(CurrentPositionProperty, new Vector2(pixelX, pixelY));
        }

        async Task ShowBitmapOnTargetAsync(CanvasBitmap bitmap)
        {
            int scale = (int)Math.Ceiling(ActualSize.Y / bitmap.Size.Height);

            Height = bitmap.Size.Height / bitmap.Size.Width * ActualSize.X;

            _previousBitmap = bitmap;
            _previousColors = null;

            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            var compositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, CanvasDevice.GetSharedDevice());

            if (_patternBitmap is null)
            {
                _patternBitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), @"Assets\BackgroundPattern.png");
            }

            var surface = compositionGraphicsDevice.CreateDrawingSurface(
                new Size(bitmap.Size.Width * scale, bitmap.Size.Height * scale),
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                DirectXAlphaMode.Premultiplied);

            using (var drawingSession = CanvasComposition.CreateDrawingSession(surface))
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

            var surfaceBrush = compositor.CreateSurfaceBrush(surface);
            _spriteVisual.Brush = surfaceBrush;
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
            if (size.Height <= 0 || size.Width <= 0)
            {
                return;
            }

            if (_framePool is not null && _session is not null)
            {
                // Unsubscribe from old framePool
                _framePool.FrameArrived -= OnFrameArrived;

                // Dispose old session and framePool
                _session.Dispose();
                _framePool.Dispose();
            }

            _canvasDevice = CanvasDevice.GetSharedDevice();

            // Create a frame pool with room for only 1 frame because we're getting a single frame, not a video.
            _framePool =
                Direct3D11CaptureFramePool.Create(_canvasDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);

            _session = _framePool.CreateCaptureSession(GraphicsCaptureItem.CreateFromVisual(_capturedVisual));

            _framePool.FrameArrived += OnFrameArrived;

            _session.StartCapture();
        }
    }
}
