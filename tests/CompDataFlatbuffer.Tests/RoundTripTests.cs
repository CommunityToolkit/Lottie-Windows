// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using Xunit;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Verifies that a graph survives a trip through the FlatBuffer format unchanged.
    /// </summary>
    /// <remarks>
    /// This is the test that the FlatBuffer based player produces the same visual tree
    /// as the JSON based one. The JSON path is Lottie file to translator to
    /// WinCompData graph. The FlatBuffer path is that same graph serialized and read
    /// back. If the two graphs are identical then any interpreter that walks one
    /// produces the same visuals as an interpreter that walks the other, so the
    /// comparison is made once here rather than being repeated for every player.
    /// </remarks>
    public class RoundTripTests
    {
        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void RoundTripPreservesTheGraph(string fileName)
        {
            var translated = Corpus.Translate(fileName);

            var expected = CompositionTreeDumper.Dump(translated.RootVisual);
            var actual = CompositionTreeDumper.Dump(CompositionDeserializer.Deserialize(translated.Serialize()));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void SerializationIsDeterministic(string fileName)
        {
            var translated = Corpus.Translate(fileName);

            // The same graph must always produce the same bytes, otherwise builds are
            // not reproducible and the output cannot be cached or diffed.
            Assert.Equal(translated.Serialize(), translated.Serialize());
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void RoundTripIsStable(string fileName)
        {
            var translated = Corpus.Translate(fileName);

            // Serializing a deserialized graph must reproduce the original bytes. This
            // catches any state that the deserializer drops but the dump does not cover.
            var once = translated.Serialize();
            var twice = CompositionSerializer.Serialize(
                CompositionDeserializer.Deserialize(once),
                translated.RequiredUapVersion,
                translated.Metadata,
                translated.PropertyBindings,
                translated.Width,
                translated.Height);

            Assert.Equal(once, twice);
        }

        [Theory]
        [MemberData(nameof(Corpus.FileNames), MemberType = typeof(Corpus))]
        public void DeserializedGraphSharesNodes(string fileName)
        {
            var translated = Corpus.Translate(fileName);
            var root = CompositionDeserializer.Deserialize(translated.Serialize());

            // A node that is reached by more than one path must be one object, not a
            // copy per path, otherwise a large animation would expand enormously and
            // animations bound to a shared object would stop being shared.
            Assert.Equal(CountDistinct(translated.RootVisual), CountDistinct(root));
        }

        // Counts the distinct visuals reachable from a root.
        static int CountDistinct(Visual root)
        {
            var seen = new System.Collections.Generic.HashSet<Visual>(
                ReferenceEqualityComparer<Visual>.Instance);

            void Walk(Visual visual)
            {
                if (!seen.Add(visual))
                {
                    return;
                }

                if (visual is ContainerVisual container)
                {
                    foreach (var child in container.Children)
                    {
                        Walk(child);
                    }
                }
            }

            Walk(root);
            return seen.Count;
        }

        sealed class ReferenceEqualityComparer<T> : System.Collections.Generic.IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
