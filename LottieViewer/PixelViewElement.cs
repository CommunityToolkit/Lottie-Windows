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
    public sealed class PixelViewElement : UserControl
    {
        readonly SpriteVisual _visual;
        bool _comparisonIsUnderway = false;
        bool _comparisonIsQueued = false;

        // Checkerboard pattern bitmap.
        static CanvasBitmap? _patternBitmap = null;

        // Previously rendered bitmap info.
        CanvasBitmap? _previousBitmap = null;
        Color[]? _previousColors = null;
        int _previousScale = 1;

        Color? _currentColor = null;

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

        public string CurrentColorString
        {
            get { return (string)GetValue(CurrentColorStringProperty); }
            set { SetValue(CurrentColorStringProperty, value); }
        }

        public Vector2 CurrentPosition
        {
            get { return (Vector2)GetValue(CurrentPositionProperty); }
            set { SetValue(CurrentPositionProperty, value); }
        }

        public PixelViewElement()
        {
            var c = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _visual = c.CreateSpriteVisual();
            _visual.RelativeSizeAdjustment = Vector2.One;
            ElementCompositionPreview.SetElementChildVisual(this, _visual);
        }

        public void OnMouseMove(object sender, PointerRoutedEventArgs e)
        {
            var position = e.GetCurrentPoint(this).Position;
            if (_previousScale < 0 || _previousBitmap == null || ActualHeight <= 0)
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

        public async Task UpdatePixelViewAsync(FrameworkElement element)
        {
            _comparisonIsQueued = true;

            if (!_comparisonIsUnderway)
            {
                _comparisonIsUnderway = true;
                while (_comparisonIsQueued)
                {
                    _comparisonIsQueued = false;
                    try
                    {
                        var bitmap = await RenderElementToBitmapAsync(element);
                        await ShowBitmapOnTargetAsync(bitmap);
                    }
                    catch { }
                }

                _comparisonIsUnderway = false;
            }
        }

        async Task ShowBitmapOnTargetAsync(CanvasBitmap bitmap)
        {
            int scale = (int)Math.Ceiling(ActualSize.Y / bitmap.Size.Height);

            Height = bitmap.Size.Height / bitmap.Size.Width * ActualSize.X;

            _previousBitmap = bitmap;
            _previousScale = scale;
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
            _visual.Brush = surfaceBrush;
        }

        /// <summary>
        /// Renders a bitmap of the given <see cref="FrameworkElement"/>.
        /// </summary>
        static Task<CanvasBitmap> RenderElementToBitmapAsync(FrameworkElement element)
            => RenderVisualToBitmapAsync(
                ElementCompositionPreview.GetElementVisual(element),
                new SizeInt32 { Width = (int)element.ActualWidth, Height = (int)element.ActualHeight });

        /// <summary>
        /// Renders a the given <see cref="Visual"/> to a <see cref="CanvasBitmap"/>. If <paramref name="size"/> is not
        /// specified, uses the size of <paramref name="visual"/>.
        /// </summary>
        static async Task<CanvasBitmap> RenderVisualToBitmapAsync(Visual visual, SizeInt32? size = null)
        {
            visual.BorderMode = CompositionBorderMode.Soft;

            // Get an object that enables capture from a visual.
            var graphicsItem = GraphicsCaptureItem.CreateFromVisual(visual);

            var canvasDevice = CanvasDevice.GetSharedDevice();

            var tcs = new TaskCompletionSource<CanvasBitmap>();

            // Create a frame pool with room for only 1 frame because we're getting a single frame, not a video.
            const int numberOfBuffers = 1;
            using (var framePool = Direct3D11CaptureFramePool.Create(
                                canvasDevice,
                                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                numberOfBuffers,
                                size ?? graphicsItem.Size))
            {
                void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
                {
                    using (var frame = sender.TryGetNextFrame())
                    {
                        tcs.SetResult(CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, frame.Surface));
                    }
                }

                using (var session = framePool.CreateCaptureSession(graphicsItem))
                {
                    framePool.FrameArrived += OnFrameArrived;

                    // Start capturing. The FrameArrived event will occur shortly.
                    session.StartCapture();

                    // Wait for the frame to arrive.
                    var result = await tcs.Task;

                    // !!!!!!!! NOTE !!!!!!!!
                    // This thread is now running inside the OnFrameArrived callback method.

                    // Unsubscribe now that we have captured the frame.
                    framePool.FrameArrived -= OnFrameArrived;

                    // Yield to allow the OnFrameArrived callback to unwind so that it is safe to
                    // Dispose the session and framepool.
                    await Task.Yield();
                }
            }

            return await tcs.Task;
        }
    }
}
