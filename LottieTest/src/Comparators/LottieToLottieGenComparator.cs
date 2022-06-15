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
using CommunityToolkit.WinUI.Lottie.LottieGen;

namespace LottieTest.Comparators
{

    static internal class LottieToLottieGenComparator
    {
        private static IEnumerable<float> ProgressToCapture(int samples)
        {
            for (int i = 0; i < samples; i++)
                yield return i / (float)(samples);
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

        public static async IAsyncEnumerable<CanvasBitmapDiff> Compare(StorageFile lottieFile, string folder, int samples = 10)
        {
            var options = CommandLineOptions.ParseCommandLine(new string[] { "-Language", "cs", "-MinimumUapVersion", "13", "-WinUIVersion", "2.7" });
            var reporter = new Reporter(TextWriter.Null, TextWriter.Null);
            LottieJsonFileProcessor.ProcessLottieJsonFile(options, reporter, lottieFile.Path, lottieFile.Path, "CodeGen", File.OpenRead(lottieFile.Path), folder, DateTime.Now);

            var code = await StorageFile.GetFileFromPathAsync($@"{folder}CodeGen.cs");

            var lottieFrames = LottieReader.ReadFrames(lottieFile, ProgressToCapture(samples), new SizeInt32 { Width = 512, Height = 512 });
            var lottieFrames2 = LottieReader.ReadFramesFromCode(code, ProgressToCapture(samples), new SizeInt32 { Width = 512, Height = 512 });

            await foreach (var (lottieFrame, lottieFrame2) in Zip(lottieFrames, lottieFrames2))
            {
                yield return new CanvasBitmapDiff(lottieFrame, lottieFrame2);
            }
        }
    }
}
