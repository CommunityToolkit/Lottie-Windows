using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LottieTest.Readers;
using Windows.Storage;

namespace LottieTest.Comparators
{
    static internal class LottieToImageSeqenceComparator
    {

        private static IEnumerable<float> ProgressToCapture(IEnumerable<int> frames, int framesNumber)
        {
            foreach (int frameIndex in frames)
                yield return ((float)frameIndex) / (float)(framesNumber);
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
        public static async IAsyncEnumerable<CanvasBitmapDiff> Compare(StorageFile lottieFile, StorageFolder sequenceFolder, int numberOfFrames)
        {
            var frameSize = await ImageSequenceReader.ReadFrameSize(sequenceFolder);

            var frames = await ImageSequenceReader.ReadFrameIndices(sequenceFolder);
            var gifFrames = ImageSequenceReader.ReadFrames(sequenceFolder, frames);
            var lottieFrames = LottieReader.ReadFrames(lottieFile, ProgressToCapture(frames, numberOfFrames), frameSize);

            await foreach (var (gifFrame, lottieFrame) in Zip(gifFrames, lottieFrames))
            {
                yield return new CanvasBitmapDiff(gifFrame, lottieFrame);
            }
        }
    }
}
