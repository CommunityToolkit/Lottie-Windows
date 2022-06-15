using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Graphics.DirectX;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.System.WinRT.Direct3D11;
using Windows.Win32.Graphics.Direct3D11;
using WinRT;
using static Windows.Win32.PInvoke;
using System.Runtime.CompilerServices;
using Windows.Storage;
using LottieTest.Comparators;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Net;

namespace LottieTest
{
    internal class LottieTest
    {
        static string TEST_FILES_FOLDER = @"D:\LottieDev\Lottie-Windows\LottieTest\Tests\";

        static async Task Main(string[] args)
        {
            Bootstrap.Initialize(0x00010000);
            await RunTests();
            Bootstrap.Shutdown();
        }

        static async Task<bool> RunTests()
        {
            var workDirectory = @"D:\LottieDev\Lottie-Windows\LottieTest\Tests\";
            var testPlanFile = await StorageFile.GetFileFromPathAsync($"{workDirectory}TestPlan.json");
            var testPlanJson = JsonNode.Parse(new StreamReader((await testPlanFile.OpenReadAsync()).AsStreamForRead()).ReadToEnd())!;

            bool allPassed = true;
            allPassed &= await RunImageSequenceTests(testPlanJson["image-sequence-tests"]!.AsArray());
            allPassed &= await RunLottieFilesGifTests(testPlanJson["lottifiles-gif-tests"]!.AsArray());
            //allPassed &= await RunLottieGenTests(testPlanJson["image-sequence-tests"]!.AsArray());
            return allPassed;
        }

        static async Task<bool> RunImageSequenceTests(JsonArray tests)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=======================================================");
            Console.WriteLine("= After Effects image sequence against Lottie-Windows =");
            Console.WriteLine("=======================================================");

            bool allPassed = true;
            foreach (var test in tests)
            {
                var name = test!["name"]!.AsValue().GetValue<string>();
                var threshold = test!["threshold"]!.AsValue().GetValue<float>();
                var avgThreshold = test!["avg-threshold"]!.AsValue().GetValue<float>();
                allPassed &= await RunImageToSequenceTest(name, threshold, avgThreshold);
            }
            return allPassed;
        }

        static async Task<bool> RunLottieFilesGifTests(JsonArray tests)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=======================================================");
            Console.WriteLine("=       LottieFiles gifs against Lottie-Windows       =");
            Console.WriteLine("=======================================================");

            bool allPassed = true;
            foreach (var test in tests)
            {
                var name = test!["name"]!.AsValue().GetValue<string>();
                var link = test!["link"]!.AsValue().GetValue<string>();
                var threshold = test!["threshold"]!.AsValue().GetValue<float>();
                var avgThreshold = test!["avg-threshold"]!.AsValue().GetValue<float>();
                allPassed &= await RunLottieFilesGifTest(name, link, threshold, avgThreshold);
            }
            return allPassed;
        }

        static async Task<bool> RunLottieGenTests(JsonArray tests)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=======================================================");
            Console.WriteLine("=           LottieGen against Lottie-Windows          =");
            Console.WriteLine("=======================================================");

            bool allPassed = true;
            foreach (var test in tests)
            {
                var name = test!["name"]!.AsValue().GetValue<string>();
                var threshold = test!["threshold"]!.AsValue().GetValue<float>();
                var avgThreshold = test!["avg-threshold"]!.AsValue().GetValue<float>();
                allPassed &= await RunLottieGenTest(name, threshold, avgThreshold);
            }
            return allPassed;
        }

        static async Task<bool> RunImageToSequenceTest(string name, float threshold, float avgThreshold)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            string nameFormatted = String.Format("{0, -14}", name);
            Console.WriteLine($"[Test Started][{nameFormatted}]");
            var test_folder = $@"{TEST_FILES_FOLDER}{name}\";

            var sequenceFolder = await StorageFolder.GetFolderFromPathAsync($@"{test_folder}frames\");
            var lottieFile = await StorageFile.GetFileFromPathAsync($@"{test_folder}Lottie.json");
            var testFile = await StorageFile.GetFileFromPathAsync($@"{test_folder}Test.json");

            var testJson = JsonNode.Parse(new StreamReader((await testFile.OpenReadAsync()).AsStreamForRead()).ReadToEnd())!;

            var results_folder = $@"{TEST_FILES_FOLDER}!Results\{name}\";

            System.IO.Directory.CreateDirectory(results_folder);

            float maxScore = 0;
            float averageScore = 0;
            int i = 0;

            await foreach (var diffItem in LottieToImageSeqenceComparator.Compare(lottieFile, sequenceFolder, testJson["number-of-frames"]!.GetValue<int>()))
            {
                /*await diffItem.GetDiffCanvasExaggerated(64).SaveAsync(results_folder + $@"diff-{i}.png");
                await diffItem.GetFirstCanvas().SaveAsync(results_folder + $@"f-{i}.png");
                await diffItem.GetSecondCanvas().SaveAsync(results_folder + $@"s-{i}.png");*/

                await Task.Delay(1);

                float numerator = diffItem.GetNumberOfDifferentPixels(64);
                float denumerator = diffItem.GetNumberOfForegroundPixels() + diffItem.GetNumberOfPixels() / 5;
                float score = numerator / denumerator;
                maxScore = (float)Math.Max(maxScore, score);
                averageScore += score;
                i++;
            }
            averageScore /= i;

            bool passed = maxScore < threshold && averageScore < avgThreshold;

            stopwatch.Stop();
            Console.ForegroundColor = (passed ? ConsoleColor.Green : ConsoleColor.Red);
            Console.WriteLine($"[{(passed ? "Test Passed" : "Test Failed")} ][{nameFormatted}] Worst diff: {maxScore * 100:0.0}% | Avg diff: {averageScore * 100:0.0}% | {stopwatch.ElapsedMilliseconds / 1e3: 0.00}s");

            return passed;
        }

        static async Task<bool> RunLottieFilesGifTest(string name, string link, float threshold, float avgThreshold)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            string nameFormatted = String.Format("{0, -14}", name);
            Console.WriteLine($"[Test Started][{nameFormatted}]");

            var test_folder = $@"{TEST_FILES_FOLDER}{name}\";

            var gifFile = await StorageFile.GetFileFromPathAsync($@"{test_folder}Animation.gif");
            var lottieFile = await StorageFile.GetFileFromPathAsync($@"{test_folder}Lottie.json");

            var results_folder = $@"{TEST_FILES_FOLDER}!Results\{name}\";

            System.IO.Directory.CreateDirectory(results_folder);

            float maxScore = 0;
            float averageScore = 0;
            int i = 0;

            await foreach (var diffItem in LottieToGifComparator.Compare(lottieFile, gifFile, 30))
            {
                /*await diffItem.GetDiffCanvasExaggerated(64).SaveAsync(results_folder + $@"diff-{i}.png");
                await diffItem.GetFirstCanvas().SaveAsync(results_folder + $@"f-{i}.png");
                await diffItem.GetSecondCanvas().SaveAsync(results_folder + $@"s-{i}.png");*/

                await Task.Delay(1);

                float numerator = diffItem.GetNumberOfDifferentPixels(64);
                float denumerator = diffItem.GetNumberOfForegroundPixels() + diffItem.GetNumberOfPixels() / 5;
                float score = numerator / denumerator;
                maxScore = (float)Math.Max(maxScore, score);
                averageScore += score;
                i++;
            }
            averageScore /= i;

            bool passed = maxScore < threshold && averageScore < avgThreshold;

            stopwatch.Stop();
            Console.ForegroundColor = (passed ? ConsoleColor.Green : ConsoleColor.Red);
            Console.WriteLine($"[{(passed ? "Test Passed" : "Test Failed")} ][{nameFormatted}] Worst diff: {maxScore * 100:0.0}% | Avg diff: {averageScore * 100:0.0}% | {stopwatch.ElapsedMilliseconds / 1e3: 0.00}s");

            return passed;
        }

        static async Task<bool> RunLottieGenTest(string name, float threshold, float avgThreshold)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            string nameFormatted = String.Format("{0, -14}", name);
            Console.WriteLine($"[Test Started][{nameFormatted}]");

            var test_folder = $@"{TEST_FILES_FOLDER}{name}\";

            var lottieFile = await StorageFile.GetFileFromPathAsync($@"{test_folder}Lottie.json");

            var results_folder = $@"{TEST_FILES_FOLDER}!Results\{name}\";

            System.IO.Directory.CreateDirectory(results_folder);

            float maxScore = 0;
            float averageScore = 0;
            int i = 0;

            await foreach (var diffItem in LottieToLottieGenComparator.Compare(lottieFile, test_folder, 30))
            {
                await diffItem.GetDiffCanvasExaggerated(64).SaveAsync(results_folder + $@"diff-{i}.png");
                await diffItem.GetFirstCanvas().SaveAsync(results_folder + $@"f-{i}.png");
                await diffItem.GetSecondCanvas().SaveAsync(results_folder + $@"s-{i}.png");

                await Task.Delay(1);

                float numerator = diffItem.GetNumberOfDifferentPixels(64);
                float denumerator = diffItem.GetNumberOfForegroundPixels() + diffItem.GetNumberOfPixels() / 5;
                float score = numerator / denumerator;
                maxScore = (float)Math.Max(maxScore, score);
                averageScore += score;
                i++;
            }
            averageScore /= i;

            bool passed = maxScore < threshold && averageScore < avgThreshold;

            stopwatch.Stop();
            Console.ForegroundColor = (passed ? ConsoleColor.Green : ConsoleColor.Red);
            Console.WriteLine($"[{(passed ? "Test Passed" : "Test Failed")} ][{nameFormatted}] Worst diff: {maxScore * 100:0.0}% | Avg diff: {averageScore * 100:0.0}% | {stopwatch.ElapsedMilliseconds / 1e3: 0.00}s");

            return passed;
        }
    }
}