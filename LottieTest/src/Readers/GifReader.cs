using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace LottieTest.Readers
{
    static internal class GifReader
    {
        public static async IAsyncEnumerable<CanvasBitmap> ReadFrames(StorageFile file, IEnumerable<int> frameIndices)
        {
            CanvasDevice _canvasDevice = CanvasDevice.GetSharedDevice();

            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, stream);
                var width = decoder.PixelWidth;
                var height = decoder.PixelHeight;

                var numFrames = decoder.FrameCount;

                var j = 0;
                var bytes = new byte[width * height * 4];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = 255;
                foreach (int i in frameIndices)
                {
                    while (j < i)
                    {
                        await DecodeBitmapFrameAsync(width, height, await decoder.GetFrameAsync((uint)j++), bytes);
                    }
                    await DecodeBitmapFrameAsync(width, height, await decoder.GetFrameAsync((uint)i), bytes);
                    yield return CanvasBitmap.CreateFromBytes(_canvasDevice, bytes, (int)width, (int)height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                }
            }
        }

        private static async Task DecodeBitmapFrameAsync(uint width, uint height, BitmapFrame frame, byte[] bytes)
        {
            var properties = await frame.BitmapProperties.GetPropertiesAsync(new string[]
                    {
                        "/imgdesc/Left",
                        "/imgdesc/Top",
                    });

            var frameLeft = (ushort)properties["/imgdesc/Left"].Value;
            var frameTop = (ushort)properties["/imgdesc/Top"].Value;


            var frameWidth = frame.PixelWidth;
            var frameHeight = frame.PixelHeight;
            var pixels = await frame.GetPixelDataAsync();
            var new_bytes = pixels.DetachPixelData();

            var format = frame.BitmapPixelFormat;
            switch (format)
            {
                case BitmapPixelFormat.Bgra8:
                    // Do nothing, it's in a format we like
                    break;
                case BitmapPixelFormat.Rgba8:
                    // Swizzle the bits
                    for (var i = 0; i < new_bytes.Length; i += 4)
                    {
                        var r = new_bytes[i + 0];
                        new_bytes[i + 0] = new_bytes[i + 2];
                        new_bytes[i + 2] = r;
                    }
                    break;
                default:
                    throw new Exception($"Unknown pixel format ({format})!");
            }

            for (int i = 0; i < frameWidth; i++)
            {
                for (int j = 0; j < frameHeight; j++)
                {
                    int x = i + frameLeft;
                    int y = j + frameTop;
                    int from = j * (int)frameWidth + i;
                    int to = y * (int)width + x;
                    if (new_bytes[from * 4 + 3] == 255)
                    {
                        bytes[to * 4] = new_bytes[from * 4];
                        bytes[to * 4 + 1] = new_bytes[from * 4 + 1];
                        bytes[to * 4 + 2] = new_bytes[from * 4 + 2];
                    }
                }
            }
        }

        public static async Task<SizeInt32> ReadFrameSize(StorageFile file)
        {
            var frames = new List<CanvasBitmap>();
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, stream);
                return new SizeInt32 { Width = (int)decoder.PixelWidth, Height = (int)decoder.PixelHeight };
            }
        }

        public static async Task<int> ReadNumberOfFrames(StorageFile file)
        {
            var frames = new List<CanvasBitmap>();
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, stream);
                return (int)decoder.FrameCount;
            }
        }
    }
}
