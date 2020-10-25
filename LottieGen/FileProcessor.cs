// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Toolkit.Uwp.UI.Lottie.DotLottie;

/// <summary>
/// Processes a file containing an animation and produces various generated outputs.
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
                    lottieFilePath: _filePath,
                    outputFolder: _outputFolder,
                    timestamp: _timestamp);
        }
    }

    static bool ProcessJsonLottieFile(
        CommandLineOptions options,
        Reporter reporter,
        string lottieFilePath,
        string outputFolder,
        DateTime timestamp)
    {
        using var jsonStream = TryOpenFile(lottieFilePath, reporter);

        if (jsonStream is null)
        {
            reporter.WriteError($"Failed to read {lottieFilePath}.");
            return false;
        }

        return LottieJsonFileProcessor.ProcessLottieFile(
            options: options,
            reporter: reporter,
            lottieFilePath: lottieFilePath,
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
            using var jsonStream = dotLottieFile.Animations[0].Open();

            if (jsonStream == null)
            {
                reporter.WriteError($"Failed to read .json from {filePath}.");
                succeeded = false;
                continue;
            }

            if (!LottieJsonFileProcessor.ProcessLottieFile(
                options: options,
                reporter: reporter,
                lottieFilePath: filePath,
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
}
