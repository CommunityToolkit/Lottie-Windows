using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Streams;

namespace LottieTest.Readers
{
    static internal class ImageSequenceReader
    {
        public static async IAsyncEnumerable<CanvasBitmap> ReadFrames(StorageFolder folder, IEnumerable<int> frameIndices)
        {
            CanvasDevice _canvasDevice = CanvasDevice.GetSharedDevice();

            int index = frameIndices.First();
            frameIndices = frameIndices.Skip(1);

            foreach (var file in await folder.GetFilesAsync())
            {
                if (!CheckFileName(file) || index != GetFrameIndex(file))
                {
                    continue;
                }

                if (frameIndices.Any())
                {
                    index = frameIndices.First();
                    frameIndices = frameIndices.Skip(1);
                }

                yield return await CanvasBitmap.LoadAsync(_canvasDevice, await file.OpenReadAsync());
            }
        }

        public static async Task<List<int>> ReadFrameIndices(StorageFolder folder)
        {
            var res = new List<int>();
            foreach (var file in await folder.GetFilesAsync())
            {
                if (!CheckFileName(file))
                {
                    continue;
                }

                res.Add(GetFrameIndex(file));
            }
            return res;
        }

        public static async Task<SizeInt32> ReadFrameSize(StorageFolder folder)
        {
            CanvasDevice _canvasDevice = CanvasDevice.GetSharedDevice();

            foreach (var file in await folder.GetFilesAsync())
            {
                if (CheckFileName(file))
                {
                    var size = (await CanvasBitmap.LoadAsync(_canvasDevice, await file.OpenReadAsync())).SizeInPixels;
                    return new SizeInt32 { Width = (int)size.Width, Height = (int)size.Height };
                }
            }

            return new SizeInt32 { Width = 512, Height = 512 };
        }

        private static bool CheckFileName(StorageFile file)
        {
            return Regex.Match(file.Name, @"frame\d+\.png").Success;
        }

        private static int GetFrameIndex(StorageFile file)
        {
            return Int32.Parse(Regex.Match(file.Name, @"frame(\d+)\.png").Groups[1].Value);
        }
    }
}
