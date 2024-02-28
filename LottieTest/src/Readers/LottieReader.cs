using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Microsoft.Graphics;
using Microsoft.Graphics.DirectX;
using Windows.Storage;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.Graphics.Canvas.UI.Composition;
using MicrosoftToolkit.WinUI.Lottie;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.System.WinRT.Direct3D11;
using Windows.Win32.Graphics.Direct3D11;
using WinRT;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Windows.Graphics;
using Windows.UI;
using static Windows.Win32.PInvoke;
using Windows.ApplicationModel.UserDataTasks;

using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace LottieTest.Readers
{
    static internal class LottieReader
    {

        public static async IAsyncEnumerable<CanvasBitmap> ReadFrames(StorageFile file, IEnumerable<float> progressValues, SizeInt32 size)
        {
            var frames = new List<CanvasBitmap>();

            var source = new LottieVisualSourceDetached();

            await source.SetSourceAsync(file);

            capture.SetSource(source, size);

            foreach (var progress in progressValues)
            {
                yield return await capture.GetFrame(progress);
            }
        }

        public static async IAsyncEnumerable<CanvasBitmap> ReadFramesFromCode(StorageFile code, IEnumerable<float> progressValue, SizeInt32 size)
        {
            // define source code, then parse it (to the type used for compilation)
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(code.Path));

            // define other necessary objects for compilation
            string assemblyName = Path.GetRandomFileName();
            MetadataReference[] references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Compositor).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Canvas).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CompositionAnimation).Assembly.Location),
            };

            // analyse and generate IL code from syntax tree
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                // write IL code into memory
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // handle exceptions
                    IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (Diagnostic diagnostic in failures)
                    {
                        Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
                else
                {
                    // load this 'virtual' DLL so that we can use
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    // create instance of the desired class and call the desired function
                    Type type = assembly.GetType("AnimatedVisuals.CodeGen")!;
                    object obj = Activator.CreateInstance(type)!;
                    type.InvokeMember("TryCreateAnimatedVisual",
                        BindingFlags.Default | BindingFlags.InvokeMethod,
                        null,
                        obj,
                        new object[]{capture.compositor!,
                       new object()});
                }
            }

            await foreach (var v in ReadFrames(code, progressValue, size))
            {
                yield return v;
            }
        }

        // This interface allows us to copy the texture
        [ComImport]
        [Guid("D4B71A65-3052-4ABE-9183-E98DE02A41A9")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface ICompositionDrawingSurfaceInterop2
        {
            unsafe void BeginDraw([Optional] RECT* updateRect, global::System.Guid* iid, [MarshalAs(UnmanagedType.IUnknown)] out object updateObject, POINT* updateOffset);
            void EndDraw();
            void Resize(SIZE sizePixels);
            unsafe void Scroll([Optional] RECT* scrollRect, [Optional] RECT* clipRect, int offsetX, int offsetY);
            void ResumeDraw();
            void SuspendDraw();
            unsafe void CopySurface([MarshalAs(UnmanagedType.IUnknown)] object destinationResource, int destinationOffsetX, int destinationOffsetY, [Optional] RECT* sourceRectangle);
        }

        static T GetDXGIInterfaceFromObject<T>(object obj)
        {
            var access = obj.As<IDirect3DDxgiInterfaceAccess>();
            object? result = null;
            unsafe
            {
                var guid = typeof(T).GUID;
                var guidPointer = (Guid*)Unsafe.AsPointer(ref guid);
                access.GetInterface(guidPointer, out result);
            }
            return result.As<T>();
        }

        class VisualCapture : IDisposable
        {
            private LottieVisualSourceDetached? visualSource;
            private SizeInt32 size = new SizeInt32 { Width = 512, Height = 512 };
            public Compositor? compositor;
            private bool sourceChanged = false;

            public VisualCapture()
            {
                Task.Run(() => Loop());
            }

            void IDisposable.Dispose()
            {
                compositor?.Dispose();
            }

            Tuple<Visual, Visual>? GetVisual()
            {
                if (visualSource is null || compositor is null)
                {
                    return null;
                }

                var av = visualSource.TryCreateAnimatedVisual(compositor, out object? diag);

                var visual = av!.RootVisual;
                var sourceSize = av!.Size;

                visual.BorderMode = CompositionBorderMode.Soft;

                ContainerVisual v = compositor.CreateContainerVisual();
                {
                    v.Size = new System.Numerics.Vector2(size.Width, size.Height);
                    var geometry = compositor.CreateRectangleGeometry();
                    geometry.Size = v.Size;
                    geometry.Offset = new Vector2(0, 0);

                    var shape = CreateSpriteShape(compositor, geometry, new Matrix3x2(1F, 0F, 0F, 1F, 0F, 0F), compositor.CreateColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)));

                    var shapeVisual = compositor.CreateShapeVisual();
                    shapeVisual.Shapes.Add(shape);
                    shapeVisual.Size = v.Size;

                    v.Children.InsertAtTop(shapeVisual);
                    v.Children.InsertAtTop(visual);

                    float scale = Math.Min(size.Width / sourceSize.X, size.Height / sourceSize.Y);

                    visual.TransformMatrix = Matrix4x4.CreateTranslation(-sourceSize.X / 2, -sourceSize.Y / 2, 0) * Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(size.Width / 2, size.Height / 2, 0);
                }

                return Tuple.Create(v as Visual, visual);
            }

            void Loop()
            {
                var dispatcherQueueController = DispatcherQueueController.CreateOnCurrentThread();
                var device = new CanvasDevice();
                compositor = new Compositor();

                var compGraphics = CanvasComposition.CreateCompositionGraphicsDevice(compositor, device);

                // We can't block the thread that created the Compositor,
                // or else the AsyncOperation will never be completed. Ask
                // the dispatcher to enqueue this work and then pump messages.


                while (!sourceChanged)
                {
                    Task.Delay(1).Wait();
                }

                var res = GetVisual()!;
                Visual lottieVisual = res.Item1;
                Visual topVisual = res.Item2;
                var renderTarget = new CanvasRenderTarget(device, size.Width, size.Height, 96);
                var resultTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(renderTarget);

                while (true)
                {
                    while (taskCompletion == null || sourceChanged)
                    {
                        if (sourceChanged)
                        {
                            res = GetVisual()!;
                            lottieVisual = res.Item1;
                            topVisual = res.Item2;
                            renderTarget = new CanvasRenderTarget(device, size.Width, size.Height, 96);
                            resultTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(renderTarget);
                            sourceChanged = false;
                        }

                        Task.Delay(1).Wait();
                    }

                    topVisual.Properties.InsertScalar("Progress", progress);

                    ICompositionSurface? surface = null;
                    dispatcherQueueController.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await compositor.RequestCommitAsync();
                        surface = await compGraphics.CaptureAsync(
                            lottieVisual,
                            size,
                            DirectXPixelFormat.B8G8R8A8UIntNormalized,
                            DirectXAlphaMode.Premultiplied, 1.0f);
                    });

                    // Pump messages until the async operation has completed.
                    var msg = new MSG();
                    while (GetMessage(out msg, new HWND(0), 0, 0) && surface == null)
                    {
                        TranslateMessage(msg);
                        DispatchMessage(msg);
                    }

                    // Now we need to copy the underlying texture from the surface.
                    var interop = surface.As<ICompositionDrawingSurfaceInterop2>();
                    unsafe
                    {
                        interop.CopySurface(resultTexture, 0, 0, null);
                    }

                    lock (mutex)
                    {
                        taskCompletion?.SetResult(CanvasBitmap.CreateFromColors(device!, renderTarget.GetPixelColors(), (int)size.Width, (int)size.Height));
                        taskCompletion = null;
                    }
                }
            }

            private object mutex = new object();

            private float progress = 0.0f;
            private TaskCompletionSource<CanvasBitmap>? taskCompletion = null;


            public async Task<CanvasBitmap> GetFrame(float progress)
            {
                lock (mutex)
                {
                    this.progress = progress;
                    taskCompletion = new TaskCompletionSource<CanvasBitmap>();
                }
                return await taskCompletion.Task;
            }

            public void SetSource(LottieVisualSourceDetached visualSource, SizeInt32 size)
            {
                this.visualSource = visualSource;
                this.size = size;
                this.sourceChanged = true;
            }
        }

        static VisualCapture capture = new VisualCapture();

        static CompositionSpriteShape CreateSpriteShape(Compositor _c, CompositionGeometry geometry, Matrix3x2 transformMatrix, CompositionBrush fillBrush)
        {
            var result = _c.CreateSpriteShape(geometry);
            result.TransformMatrix = transformMatrix;
            result.FillBrush = fillBrush;
            return result;
        }
    }
}
