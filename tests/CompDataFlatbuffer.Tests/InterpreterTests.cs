// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.LottieRuntime;
using Google.FlatBuffers;
using Windows.UI.Composition;
using Xunit;
using Fb = CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Schema;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Verifies that the managed interpreter builds the composition that the buffer
    /// describes.
    /// </summary>
    /// <remarks>
    /// The interpreter goes straight from bytes to composition objects, so unlike the
    /// deserializer it cannot be checked by comparing WinCompData graphs. Instead the
    /// tree it builds is dumped in the same canonical text as the graph that the
    /// buffer was written from, and the two are compared. Equal text means the
    /// interpreter created the same objects, gave them the same values, and shared
    /// them in the same way, which is the whole of what an interpreter has to get
    /// right.
    /// <para/>
    /// The composition APIs that the interpreter calls only exist on Windows, so this
    /// runs against the stand-ins in CompositionApiStandIns.cs. That means these tests
    /// verify the calls that the interpreter makes rather than the pixels that Windows
    /// draws; the pixels are the same as the deserializer's precisely because the
    /// calls are.
    /// </remarks>
    public class InterpreterTests
    {
        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void InterpretedTreeMatchesTheGraph(string fileName)
        {
            var translated = Corpus.Translate(fileName);

            var expected = CompositionTreeDumper.Dump(translated.RootVisual);
            var actual = InterpretedTreeDumper.Dump(Interpret(translated.Serialize()));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void InterpretationIsRepeatable(string fileName)
        {
            var buffer = Corpus.Translate(fileName).Serialize();

            // Interpreting the same buffer twice must produce the same tree. A buffer
            // is read in place, so this also shows that reading it does not modify it.
            Assert.Equal(
                InterpretedTreeDumper.Dump(Interpret(buffer)),
                InterpretedTreeDumper.Dump(Interpret(buffer)));
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void ProgressPropertySetHoldsTheProgressProperty(string fileName)
        {
            var root = Interpret(Corpus.Translate(fileName).Serialize());

            // Every translated animation is driven by a single Progress property on the
            // root visual's property set. A host that cannot find it cannot play the
            // animation, so this is checked separately from the dump.
            Assert.Contains(
                "Progress",
                CompositionInterpreter.ProgressPropertySet(root).Names.Keys,
                StringComparer.Ordinal);
        }

        [Fact]
        public void EmptyBufferIsRejected()
            => Assert.Throws<FlatBufferFormatException>(() => Interpret(Array.Empty<byte>()));

        [Fact]
        public void GarbageIsRejected()
        {
            var garbage = new byte[512];
            new Random(0).NextBytes(garbage);

            Assert.Throws<FlatBufferFormatException>(() => Interpret(garbage));
        }

        [Fact]
        public void BufferWithTheWrongFileIdentifierIsRejected()
        {
            var buffer = Corpus.Translate("LightBulb.json").Serialize();

            // The file identifier sits at offset 4, immediately after the root offset.
            buffer[4] = (byte)'X';

            Assert.Throws<FlatBufferFormatException>(() => Interpret(buffer));
        }

        [Fact]
        public void BufferWithNoRootVisualIsRejected()
            => Assert.Throws<FlatBufferFormatException>(
                () => Interpret(CreateComposition(Format.Version, 0, withRootVisual: false)));

        [Fact]
        public void NewerSchemaVersionIsRejected()
        {
            // A buffer written by a newer serializer may use fields that this build
            // does not know about, so loading it would silently lose part of the
            // animation. It is refused instead.
            var exception = Assert.Throws<NotSupportedException>(
                () => Interpret(CreateComposition((ushort)(Format.Version + 1), 0)));

            Assert.Contains($"{Format.Version + 1}", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CurrentSchemaVersionIsAccepted()
            => Assert.NotNull(Interpret(CreateComposition(Format.Version, 0)));

        [Fact]
        public void UnavailableApiContractIsRejected()
        {
            // An animation that needs composition APIs that this version of Windows
            // does not have is refused rather than built into a tree that cannot run.
            var exception = Assert.Throws<NotSupportedException>(
                () => Interpret(CreateComposition(Format.Version, ushort.MaxValue)));

            Assert.Contains("UniversalApiContract", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void TruncatedBuffersAreRejected(string fileName)
        {
            var buffer = Corpus.Translate(fileName).Serialize();

            // Truncation is checked at a spread of lengths rather than at every length,
            // which would make the test run for minutes without finding anything more.
            for (var length = 0; length < buffer.Length; length += Math.Max(1, buffer.Length / 64))
            {
                var truncated = new byte[length];
                Array.Copy(buffer, truncated, length);

                Assert.Throws<FlatBufferFormatException>(() => Interpret(truncated));
            }
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void CorruptedBuffersDoNotCrash(string fileName)
        {
            var original = Corpus.Translate(fileName).Serialize();
            var random = new Random(fileName.GetHashCode(StringComparison.Ordinal));

            // Unlike truncation, a flipped bit may leave a buffer that is still valid,
            // just describing a different animation. So the requirement here is weaker:
            // the interpreter either succeeds or reports a format error, but never
            // throws something else and never reads outside the buffer.
            for (var i = 0; i < 200; i++)
            {
                var corrupted = (byte[])original.Clone();
                corrupted[random.Next(corrupted.Length)] ^= (byte)(1 << random.Next(8));

                try
                {
                    Interpret(corrupted);
                }
                catch (FlatBufferFormatException)
                {
                }
                catch (NotSupportedException)
                {
                    // A flipped bit can also land in the schema version or the required
                    // API contract version.
                }
            }
        }

        static Visual Interpret(byte[] buffer)
            => CompositionInterpreter.LoadComposition(new Compositor(), buffer);

        // Builds the smallest possible composition: one empty container visual. Used by
        // the tests of the header, which have nothing to do with the contents.
        static byte[] CreateComposition(ushort schemaVersion, ushort requiredUapVersion, bool withRootVisual = true)
        {
            var builder = new FlatBufferBuilder(256);

            Fb.Visual.StartVisual(builder);
            Fb.Visual.AddKind(builder, Fb.VisualKind.Container);
            var visual = Fb.Visual.EndVisual(builder);

            var visuals = Fb.LottieComposition.CreateVisualsVector(builder, new[] { visual });

            Fb.LottieComposition.StartLottieComposition(builder);
            Fb.LottieComposition.AddSchemaVersion(builder, schemaVersion);
            Fb.LottieComposition.AddRequiredUapVersion(builder, requiredUapVersion);

            if (withRootVisual)
            {
                Fb.LottieComposition.AddRootVisual(builder, 0);
            }

            Fb.LottieComposition.AddVisuals(builder, visuals);
            var composition = Fb.LottieComposition.EndLottieComposition(builder);

            Fb.LottieComposition.FinishLottieCompositionBuffer(builder, composition);

            return builder.SizedByteArray();
        }
    }
}
