// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Xunit;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Verifies that malformed input is rejected rather than being allowed to
    /// corrupt the reader.
    /// </summary>
    /// <remarks>
    /// A serialized composition is likely to be loaded from a file or downloaded, so
    /// it has to be treated as untrusted. Every one of these cases must fail with a
    /// <see cref="FlatBufferFormatException"/>: no other exception type, no crash, and
    /// no hang. The native interpreter performs the same checks, so this is also a
    /// specification of what that interpreter has to reject.
    /// </remarks>
    public class RobustnessTests
    {
        [Fact]
        public void EmptyBufferIsRejected()
            => Assert.Throws<FlatBufferFormatException>(() => CompositionDeserializer.Deserialize(Array.Empty<byte>()));

        [Fact]
        public void GarbageIsRejected()
        {
            var garbage = new byte[512];
            new Random(0).NextBytes(garbage);

            Assert.Throws<FlatBufferFormatException>(() => CompositionDeserializer.Deserialize(garbage));
        }

        [Fact]
        public void BufferWithTheWrongFileIdentifierIsRejected()
        {
            var buffer = Corpus.Translate("LightBulb.json").Serialize();

            // The file identifier sits at offset 4, immediately after the root offset.
            buffer[4] = (byte)'X';

            Assert.Throws<FlatBufferFormatException>(() => CompositionDeserializer.Deserialize(buffer));
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

                Assert.Throws<FlatBufferFormatException>(() => CompositionDeserializer.Deserialize(truncated));
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
            // the reader either succeeds or reports a format error, but never throws
            // something else and never reads outside the buffer.
            for (var i = 0; i < 200; i++)
            {
                var corrupted = (byte[])original.Clone();
                corrupted[random.Next(corrupted.Length)] ^= (byte)(1 << random.Next(8));

                try
                {
                    CompositionDeserializer.Deserialize(corrupted);
                }
                catch (FlatBufferFormatException)
                {
                }
            }
        }
    }
}
