// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.CompMetadata;
using CommunityToolkit.WinUI.Lottie.LottieData;
using CommunityToolkit.WinUI.Lottie.LottieData.Serialization;
using CommunityToolkit.WinUI.Lottie.LottieMetadata;
using CommunityToolkit.WinUI.Lottie.LottieToWinComp;
using CommunityToolkit.WinUI.Lottie.WinCompData;
using IoPath = System.IO.Path;

namespace CommunityToolkit.WinUI.Lottie.CompDataFlatbuffer.Tests
{
    /// <summary>
    /// Loads the Lottie files that the tests run against and translates them to
    /// WinCompData graphs.
    /// </summary>
    static class Corpus
    {
        // The well known keys under which the translator stores its metadata. These are
        // the same values that LottieGen uses to retrieve it.
        static readonly Guid LottieMetadataKey = new Guid("EA3D6538-361A-4B1C-960D-50A6C35DC0B4");
        static readonly Guid PropertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        /// <summary>
        /// Gets the names of the Lottie files that the tests run against.
        /// </summary>
        /// <returns>A test case per Lottie file.</returns>
        public static IEnumerable<object[]> FileNames()
            => Directory.EnumerateFiles(AssetsFolder, "*.json")
                .Select(IoPath.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name! });

        static string AssetsFolder
        {
            get
            {
                // The tests run from the build output folder, so the repository root is
                // found by walking up to the folder that contains the samples.
                var directory = new DirectoryInfo(AppContext.BaseDirectory);

                while (directory is not null)
                {
                    var candidate = IoPath.Combine(directory.FullName, "LottieSamples", "Assets");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }

                throw new InvalidOperationException("Could not locate LottieSamples/Assets.");
            }
        }

        /// <summary>
        /// Reads and translates one of the Lottie files.
        /// </summary>
        /// <param name="fileName">The name of the file within the samples folder.</param>
        /// <returns>The translated graph and the metadata that accompanies it.</returns>
        public static TranslatedLottie Translate(string fileName)
        {
            using var stream = File.OpenRead(IoPath.Combine(AssetsFolder, fileName));

            var lottie = LottieCompositionReader.ReadLottieCompositionFromJsonStream(
                stream,
                LottieCompositionReader.Options.IgnoreMatchNames,
                out var readerIssues);

            if (lottie is null)
            {
                throw new InvalidOperationException(
                    $"Failed to read {fileName}: {string.Join("; ", readerIssues.Select(i => i.Description))}");
            }

            var result = LottieToWinCompTranslator.TryTranslateLottieComposition(
                lottie,
                new TranslatorConfiguration
                {
                    AddCodegenDescriptions = true,
                    TranslatePropertyBindings = true,

                    // The newest version is targeted so that the translation uses every
                    // feature it can, which gives the tests the widest coverage.
                    TargetUapVersion = uint.MaxValue,
                });

            if (result.RootVisual is null)
            {
                throw new InvalidOperationException($"Failed to translate {fileName}.");
            }

            return new TranslatedLottie(
                result.RootVisual,
                checked((ushort)result.MinimumRequiredUapVersion),
                result.SourceMetadata.TryGetValue(LottieMetadataKey, out var metadata)
                    ? (LottieCompositionMetadata)metadata
                    : null,
                result.SourceMetadata.TryGetValue(PropertyBindingNamesKey, out var bindings)
                    ? (IReadOnlyList<PropertyBinding>)bindings
                    : null,
                (float)lottie.Width,
                (float)lottie.Height);
        }

        /// <summary>
        /// A translated Lottie file, and everything needed to serialize it.
        /// </summary>
        /// <param name="RootVisual">The root of the translated graph.</param>
        /// <param name="RequiredUapVersion">The minimum UAP version needed to instantiate the graph.</param>
        /// <param name="Metadata">Metadata about the source animation.</param>
        /// <param name="PropertyBindings">The property bindings that the graph exposes.</param>
        /// <param name="Width">The width of the source animation.</param>
        /// <param name="Height">The height of the source animation.</param>
        public sealed record TranslatedLottie(
            Visual RootVisual,
            ushort RequiredUapVersion,
            LottieCompositionMetadata? Metadata,
            IReadOnlyList<PropertyBinding>? PropertyBindings,
            float Width,
            float Height)
        {
            /// <summary>
            /// Serializes the graph.
            /// </summary>
            /// <returns>The serialized graph.</returns>
            public byte[] Serialize()
                => CompositionSerializer.Serialize(
                    RootVisual, RequiredUapVersion, Metadata, PropertyBindings, Width, Height);
        }
    }
}
