// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.DotLottie;

/// <summary>
/// Processes a file containing Lottie animations and produces various generated outputs.
/// Both JSON and .lottie files are supported.
/// </summary>
sealed class FileProcessor
{
    readonly CommandLineOptions _options;
    readonly Reporter _reporter;
    readonly string _filePath;
    readonly string _outputFolder;
    readonly DateTime _timestamp;

    FileProcessor(
        CommandLineOptions options,
        Reporter reporter,
        string filePath,
        string outputFolder,
        DateTime timestamp)
    {
        _options = options;
        _filePath = filePath;
        _reporter = reporter;
        _outputFolder = outputFolder;
        _timestamp = timestamp;
    }

    internal static bool ProcessFile(
        CommandLineOptions options,
        Reporter reporter,
        string filePath,
        string outputFolder,
        DateTime timestamp)
    {
        try
        {
            return new FileProcessor(options, reporter, filePath, outputFolder, timestamp).Run();
        }
        catch
        {
            reporter.WriteError($"Unhandled exception processing: {filePath}");
            throw;
        }
    }

    bool Run()
    {
        switch (Path.GetExtension(_filePath).ToLowerInvariant())
        {
            case ".lottie":
                return ProcessDotLottieFile(
                    options: _options,
                    reporter: _reporter,
                    filePath: _filePath,
                    outputFolder: _outputFolder,
                    timestamp: _timestamp);

            case ".json":
            default:
                return ProcessJsonLottieFile(
                    options: _options,
                    reporter: _reporter,
                    jsonFilePath: _filePath,
                    outputFolder: _outputFolder,
                    timestamp: _timestamp);
        }
    }

    static bool ProcessJsonLottieFile(
        CommandLineOptions options,
        Reporter reporter,
        string jsonFilePath,
        string outputFolder,
        DateTime timestamp)
    {
        using var jsonStream = TryOpenFile(jsonFilePath, reporter);

        if (jsonStream is null)
        {
            reporter.WriteError($"Failed to read {jsonFilePath}.");
            return false;
        }

        return LottieJsonFileProcessor.ProcessLottieJsonFile(
            options: options,
            reporter: reporter,
            sourceFilePath: jsonFilePath,
            jsonFilePath: jsonFilePath,
            className: GetClassName(jsonFilePath),
            jsonStream: jsonStream,
            outputFolder: outputFolder,
            timestamp: timestamp);
    }

    static bool ProcessDotLottieFile(
        CommandLineOptions options,
        Reporter reporter,
        string filePath,
        string outputFolder,
        DateTime timestamp)
    {
        using var fileStream = TryOpenFile(filePath, reporter);

        if (fileStream is null)
        {
            return false;
        }

        ZipArchive zipArchive;
        try
        {
            zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException e)
        {
            using (reporter.ErrorStream.Lock())
            {
                reporter.WriteError($"File {filePath} is not a valid .lottie file.");
                reporter.WriteError(e.Message);
            }

            return false;
        }

        var dotLottieFile = DotLottieFile.FromZipArchive(zipArchive);

        if (dotLottieFile is null)
        {
            reporter.WriteError($"File {filePath} is not a valid .lottie file.");
            return false;
        }

        if (dotLottieFile.Animations.Count == 0)
        {
            reporter.WriteError($"File {filePath} contains no animations.");
            return false;
        }

        var succeeded = true;
        foreach (var animation in dotLottieFile.Animations)
        {
            var jsonPath = $"{filePath}{animation.Path}";

            using var jsonStream = animation.Open();

            if (jsonStream == null)
            {
                reporter.WriteError($"Failed to read from {jsonPath}.");
                succeeded = false;
                continue;
            }

            // The name of the class should reflect the name of the .lottie file,
            // but if there are multiple .json files in the .lottie file then we
            // need to differentiate them by adding the file name.
            var className = dotLottieFile.Animations.Count == 1
                ? GetClassName(filePath)
                : GetClassName($"{filePath}_{animation.Id}");

            if (!LottieJsonFileProcessor.ProcessLottieJsonFile(
                options: options,
                reporter: reporter,
                sourceFilePath: filePath,
                jsonFilePath: jsonPath,
                className: className,
                jsonStream: jsonStream,
                outputFolder: outputFolder,
                timestamp))
            {
                succeeded = false;
            }
        }

        return succeeded;
    }

    static Stream? TryOpenFile(string filePath, Reporter reporter)
    {
        using (reporter.InfoStream.Lock())
        {
            reporter.WriteInfo($"Reading file:");
            reporter.WriteInfo(InfoType.FilePath, $" {filePath}");
        }

        try
        {
            return File.OpenRead(filePath);
        }
        catch (Exception e)
        {
            using (reporter.ErrorStream.Lock())
            {
                reporter.WriteError($"Failed to read from {filePath}");
                reporter.WriteError(e.Message);
            }

            return null;
        }
    }

    // Get an appropriate name for a generated class.
    static string GetClassName(string path) =>
        TrySynthesizeClassName(Path.GetFileNameWithoutExtension(path)) ??
        "Lottie";  // If all else fails, just call it Lottie.

    /// <summary>
    /// Takes a name and modifies it as necessary to be suited for use as a class name in languages such
    /// as  C# and C++.
    /// Returns null on failure.
    /// </summary>
    /// <returns>A name, or null.</returns>
    static string? TrySynthesizeClassName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Remove any leading punctuation.
        var prefixSize = name.TakeWhile(c => !char.IsLetterOrDigit(c)).Count();

        return SanitizeTypeName(name.Substring(prefixSize));
    }

    // Makes the given name suitable for use as a class name in languages such as C# and C++.
    static string? SanitizeTypeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // If the first character is not a letter, prepend an underscore.
        if (!char.IsLetter(name, 0))
        {
            name = "_" + name;
        }

        // Replace any disallowed character with underscores.
        name =
            new string((from ch in name
                        select char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

        // Remove any duplicated underscores.
        name = name.Replace("__", "_");

        // Capitalize the first letter.
        name = name.ToUpperInvariant().Substring(0, 1) + name.Substring(1);

        return name;
    }
}
