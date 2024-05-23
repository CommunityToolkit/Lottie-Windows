using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LottieTest.Readers;
using Windows.Graphics;
using Windows.Media.Devices;
using Windows.Storage;

namespace LottieTest.Comparators
{

    static internal class LottieToGifComparator
    {
        private static IEnumerable<int> FramesToCapture(int framesNumber, int samples)
        {
            for (int i = 0; i < framesNumber - 1; i += Math.Max(1, (framesNumber - 1) / (samples - 1)))
                yield return i;
        }

        private static IEnumerable<float> ProgressToCapture(int framesNumber, int samples)
        {
            foreach (int frameIndex in FramesToCapture(framesNumber, samples))
                yield return (frameIndex) / (float)(framesNumber);
        }

        public static async IAsyncEnumerable<(TFirst, TSecond)> Zip<TFirst, TSecond>(this IAsyncEnumerable<TFirst> first, IAsyncEnumerable<TSecond> second)
        {
            await using var e1 = first.GetAsyncEnumerator();
            await using var e2 = second.GetAsyncEnumerator();

            while (true)
            {
                var t1 = e1.MoveNextAsync().AsTask();
                var t2 = e2.MoveNextAsync().AsTask();
                await Task.WhenAll(t1, t2);

                if (!t1.Result || !t2.Result)
                    yield break;

                yield return (e1.Current, e2.Current);
            }
        }

        public static async IAsyncEnumerable<CanvasBitmapDiff> Compare(StorageFile lottieFile, StorageFile gifFile, int samples = 10)
        {
            int numberOfFrames = await GifReader.ReadNumberOfFrames(gifFile);
            var frameSize = await GifReader.ReadFrameSize(gifFile);

            var gifFrames = GifReader.ReadFrames(gifFile, FramesToCapture(numberOfFrames, samples));
            var lottieFrames = LottieReader.ReadFrames(lottieFile, ProgressToCapture(numberOfFrames, samples), frameSize);

            await foreach (var (gifFrame, lottieFrame) in Zip(gifFrames, lottieFrames))
            {
                yield return new CanvasBitmapDiff(gifFrame, lottieFrame);
            }
        }
    }
}
