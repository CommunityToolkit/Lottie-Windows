// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommunityToolkit.WinUI.Lottie.DotLottie
{
    /// <summary>
    /// Provides access to the contents of a .lottie file.
    /// See https://dotlottie.io.
    /// </summary>
    sealed class DotLottieFile : IDisposable
    {
        readonly Manifest _manifest;

        DotLottieFile(ZipArchive zipArchive, Manifest manifest)
        {
            ZipArchive = zipArchive;
            _manifest = manifest;
            Animations = manifest.Animations.Select(a =>
                new DotLottieFileAnimation(
                        this,
                        a.Id,
                        a.Loop)).ToArray();
        }

        /// <summary>
        /// The name of the tool that generated the .lottie file.
        /// </summary>
        public string? Generator => _manifest.Generator;

        /// <summary>
        /// The .lottie file format version.
        /// </summary>
        public string Version => _manifest.Version;

        /// <summary>
        /// The author of the .lottie file.
        /// </summary>
        public string? Author => _manifest.Author;

        /// <summary>
        /// Returns the animations that are contained in the .lottie file.
        /// </summary>
        public IReadOnlyList<DotLottieFileAnimation> Animations { get; }

        /// <summary>
        /// Opens a file given its path in the .lottie file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>A stream containing the file, or null if the file is not found.</returns>
        public Stream? OpenFile(string path)
        {
            var zipEntry = GetEntryForPath(path);

            return zipEntry is null ? null : zipEntry.Open();
        }

        /// <summary>
        /// Opens a file given its path in the .lottie file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>A stream containing the file, or null if the file is not found.</returns>
        public MemoryStream? OpenFileAsMemoryStream(string path)
        {
            var zipEntry = GetEntryForPath(path);

            if (zipEntry is null)
            {
                return null;
            }

            var buffer = new byte[zipEntry.Length];
            using (var zipEntryStream = zipEntry.Open())
            {
                if (zipEntryStream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    return null;
                }
            }

            return new MemoryStream(buffer, writable: false);
        }

        /// <summary>
        /// Returns a <see cref="DotLottieFile"/> that reads from the given <see cref="ZipArchive"/>.
        /// </summary>
        /// <param name="zipArchive">The <see cref="ZipArchive"/>.</param>
        /// <returns>A <see cref="DotLottieFile"/> or null on error.</returns>
        public static DotLottieFile? FromZipArchive(
            ZipArchive zipArchive)
        {
            // The manifest contains information about the animation.
            var manifestEntry = GetManifestEntry(zipArchive);

            if (manifestEntry is null)
            {
                // Not a valid .lottie file.
                return null;
            }

            var manifestBytes = new byte[manifestEntry.Length];
            using var manifestStream = manifestEntry.Open();
            var bytesRead = manifestStream.Read(
                                    manifestBytes,
                                    0,
                                    manifestBytes.Length);
            if (bytesRead != manifestBytes.Length)
            {
                // Failed to read the manifest
                return null;
            }

            var manifest = Manifest.ParseManifest(manifestBytes);

            return manifest is null ? null : new DotLottieFile(zipArchive, manifest);
        }

        /// <summary>
        /// Returns a <see cref="DotLottieFile"/> that reads from the given <see cref="ZipArchive"/>.
        /// </summary>
        /// <param name="zipArchive">The <see cref="ZipArchive"/>.</param>
        /// <returns>A <see cref="DotLottieFile"/> or null on error.</returns>
        public static async Task<DotLottieFile?> FromZipArchiveAsync(
            ZipArchive zipArchive)
        {
            // The manifest contains information about the animation.
            var manifestEntry = GetManifestEntry(zipArchive);

            if (manifestEntry is null)
            {
                // Not a valid .lottie file.
                return null;
            }

            var manifestBytes = new byte[manifestEntry.Length];
            using var manifestStream = manifestEntry.Open();
            var bytesRead = await manifestStream.ReadAsync(
                                    manifestBytes,
                                    0,
                                    manifestBytes.Length);
            if (bytesRead != manifestBytes.Length)
            {
                // Failed to read the manifest
                return null;
            }

            var manifest = Manifest.ParseManifest(manifestBytes);

            return manifest is null ? null : new DotLottieFile(zipArchive, manifest);
        }

        public void Dispose()
        {
            ZipArchive.Dispose();
        }

        internal ZipArchive ZipArchive { get; }

        static ZipArchiveEntry? GetManifestEntry(ZipArchive zipArchive)
            => zipArchive.GetEntry("manifest.json");

        ZipArchiveEntry? GetEntryForPath(string path)
        {
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            return ZipArchive.GetEntry(path);
        }

        static void ConsumeToken(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                throw new InvalidLottieFileException();
            }
        }

        sealed class Animation
        {
            Animation(string id, double speed, string themeColor, bool loop)
            {
                Id = id;
                Speed = speed;
                ThemeColor = themeColor;
                Loop = loop;
            }

            public string Id { get; }

            public double Speed { get; }

            public string ThemeColor { get; }

            public bool Loop { get; }

            internal static Animation ParseAnimationObject(ref Utf8JsonReader reader)
            {
                string? id = null;
                var speed = 1.0;
                var loop = false;
                var themeColor = "#000000";

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            var propertyName = reader.GetString();
                            ConsumeToken(ref reader);

                            switch (propertyName)
                            {
                                case "id":
                                    id = reader.GetString();
                                    break;

                                case "speed":
                                    if (!reader.TryGetDouble(out speed))
                                    {
                                        throw new InvalidLottieFileException();
                                    }

                                    break;

                                case "themeColor":
                                    themeColor = reader.GetString() ?? themeColor;
                                    break;

                                case "loop":
                                    loop = reader.GetBoolean();
                                    break;

                                default:
                                    // Unrecognized property. Ignore.
                                    reader.Skip();
                                    break;
                            }

                            break;

                        case JsonTokenType.EndObject:
                            return id is null
                                ? throw new InvalidLottieFileException()
                                : new Animation(id, speed, themeColor, loop);

                        default:
                            throw new InvalidLottieFileException();
                    }
                }

                throw new InvalidLottieFileException();
            }
        }

        sealed class Manifest
        {
            Manifest(
                IReadOnlyList<Animation> animations,
                string generator,
                int? revision,
                string version,
                string? author)
            {
                Animations = animations;
                Generator = generator;
                Revision = revision;
                Version = version;
                Author = author;
            }

            public IReadOnlyList<Animation> Animations { get; }

            public string? Author { get; }

            public string? Generator { get; }

            public int? Revision { get; }

            public string Version { get; }

            internal static Manifest? ParseManifest(byte[] manifestBytes)
            {
                var reader = new Utf8JsonReader(
                    manifestBytes,
                    new JsonReaderOptions
                    {
                        // Be resilient about trailing commas - ignore them.
                        AllowTrailingCommas = true,

                        // Be resilient about comments - ignore them.
                        CommentHandling = JsonCommentHandling.Skip,

                        // Fail if the JSON exceeds this depth.
                        MaxDepth = 5,
                    });

                try
                {
                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.StartObject:
                                return ParseManifestObject(ref reader);
                            default:
                                return null;
                        }
                    }
                }
                catch (InvalidLottieFileException)
                {
                    // Ignore the exception and return null to indicate the error.
                }

                return null;
            }

            static Manifest? ParseManifestObject(ref Utf8JsonReader reader)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new InvalidLottieFileException();
                }

                IReadOnlyList<Animation>? animations = null;
                string? author = null;
                string? generator = null;
                int? revision = null;
                string? version = null;

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.EndObject:
                            return animations is null || version is null || generator is null
                                ? null
                                : new Manifest(
                                        animations,
                                        generator,
                                        revision,
                                        version,
                                        author);

                        case JsonTokenType.PropertyName:
                            var propertyName = reader.GetString();
                            ConsumeToken(ref reader);
                            switch (propertyName)
                            {
                                case "animations":
                                    animations = ParseAnimationsArray(ref reader);
                                    break;

                                case "author":
                                    author = reader.GetString();
                                    break;

                                case "generator":
                                    generator = reader.GetString();
                                    break;

                                case "custom":
                                    // For now we just ignore the custom object.
                                    reader.Skip();
                                    break;

                                case "version":
                                    version = reader.GetString();
                                    break;

                                case "revision":
                                    revision = reader.GetInt32();
                                    break;

                                default:
                                    // Unrecognized property. Ignore.
                                    reader.Skip();
                                    break;
                            }

                            break;

                        default:
                            throw new InvalidLottieFileException();
                    }
                }

                throw new InvalidLottieFileException();
            }

            static IReadOnlyList<Animation> ParseAnimationsArray(
                ref Utf8JsonReader reader)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new InvalidLottieFileException();
                }

                var animations = new List<Animation>();

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            var animationObject =
                                Animation.ParseAnimationObject(ref reader);
                            animations.Add(animationObject);

                            break;

                        case JsonTokenType.EndArray:
                            return animations.ToArray();

                        default:
                            throw new InvalidLottieFileException();
                    }
                }

                throw new InvalidLottieFileException();
            }
        }
    }
}
