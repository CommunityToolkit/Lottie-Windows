// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.CodeGen;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools;

/// <summary>
/// Processes a single Lottie file to produce various generated outputs.
/// </summary>
sealed class LottieFileProcessor
{
    readonly CommandLineOptions _options;
    readonly Profiler _profiler = new Profiler();
    readonly Reporter _reporter;
    readonly string _file;
    readonly string _outputFolder;
    readonly string _className;
    bool _reportedErrors;
    bool? _isTranslatedSuccessfully;
    LottieComposition _lottieComposition;
    Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats _lottieStats;
    (string Code, string Description)[] _readerIssues;
    (string Code, string Description)[] _translationIssues;
    Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats _beforeOptimizationStats;
    Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats _afterOptimizationStats;
    Visual _rootVisual;

    LottieFileProcessor(CommandLineOptions options, Reporter reporter, string file, string outputFolder)
    {
        _options = options;
        _reporter = reporter;
        _file = file;
        _outputFolder = outputFolder;

        // Get an appropriate name for a generated class.
        _className =
            InstantiatorGeneratorBase.TrySynthesizeClassName(_options.ClassName) ??
            InstantiatorGeneratorBase.TrySynthesizeClassName(Path.GetFileNameWithoutExtension(_file)) ??
            InstantiatorGeneratorBase.TrySynthesizeClassName("Lottie");  // If all else fails, just call it Lottie.
    }

    internal static bool ProcessFile(CommandLineOptions options, Reporter reporter, string file, string outputFolder)
    {
        try
        {
            return new LottieFileProcessor(options, reporter, file, outputFolder).Run();
        }
        catch
        {
            reporter.ErrorStream.WriteLine($"Unhandled exception processing: {file}");
            throw;
        }
    }

    bool Run()
    {
        // Make sure we can write to the output directory.
        if (!TryEnsureDirectoryExists(_outputFolder))
        {
            _reporter.WriteError($"Failed to create the output directory: {_outputFolder}");
            return false;
        }

        // Read the Lottie .json text.
        var jsonStream = TryReadTextFile(_file);

        if (jsonStream == null)
        {
            return false;
        }

        // Parse the Lottie.
        _lottieComposition =
            LottieCompositionReader.ReadLottieCompositionFromJsonStream(
                jsonStream,
                LottieCompositionReader.Options.IgnoreMatchNames,
                out _readerIssues);

        _profiler.OnParseFinished();

        foreach (var issue in _readerIssues)
        {
            _reporter.WriteInfo(IssueToString(_file, issue));
        }

        if (_lottieComposition == null)
        {
            _reporter.WriteError($"Failed to parse Lottie file: {_file}");
            return false;
        }

        _lottieStats = new Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats(_lottieComposition);

        var codeGenResult = TryGenerateCode();

        // Output extra information if the user specified verbose output.
        if (_options.Verbose)
        {
            if (_profiler.HasAnyResults)
            {
                _reporter.WriteInfoNewLine();
                _reporter.WriteInfo(" === Timings ===");
                _profiler.WriteReport(_reporter.InfoStream);
            }

            if (_lottieStats != null)
            {
                _reporter.WriteInfoNewLine();
                WriteLottieStatsReport(_reporter.InfoStream, _lottieStats);
            }

            if (_beforeOptimizationStats != null && _afterOptimizationStats != null)
            {
                _reporter.WriteInfoNewLine();
                WriteCodeGenStatsReport(_reporter.InfoStream, _beforeOptimizationStats, _afterOptimizationStats);
            }
        }

        // Any error that was reported is treated as a failure.
        codeGenResult &= !_reportedErrors;

        return codeGenResult;
    }

    bool TryGenerateCode()
    {
        var outputFileBase = Path.Combine(_outputFolder, Path.GetFileNameWithoutExtension(_file));

        var codeGenSucceeded = true;
        foreach (var lang in _options.Languages)
        {
            switch (lang)
            {
                case Lang.CSharp:
                    codeGenSucceeded &= TryGenerateCSharpCode($"{outputFileBase}.cs");
                    _profiler.OnCodeGenFinished();
                    break;

                case Lang.Cx:
                    codeGenSucceeded &= TryGenerateCXCode($"{outputFileBase}.h", $"{outputFileBase}.cpp");
                    _profiler.OnCodeGenFinished();
                    break;

                case Lang.LottieXml:
                    codeGenSucceeded &= TryGenerateLottieXml($"{outputFileBase}-Lottie.xml");
                    _profiler.OnSerializationFinished();
                    break;

                case Lang.LottieYaml:
                    codeGenSucceeded &= TryGenerateLottieYaml($"{outputFileBase}-Lottie.yaml");
                    _profiler.OnSerializationFinished();
                    break;

                case Lang.WinCompXml:
                    codeGenSucceeded &= TryGenerateWincompXml($"{outputFileBase}-wincomp.xml");
                    _profiler.OnSerializationFinished();
                    break;

                case Lang.WinCompDgml:
                    codeGenSucceeded &= TryGenerateWincompDgml($"{outputFileBase}.dgml");
                    _profiler.OnSerializationFinished();
                    break;

                case Lang.Stats:
                    codeGenSucceeded &= TryGenerateStats(outputFileBase);
                    break;

                default:
                    _reporter.WriteError($"Language {lang} is not supported.");
                    return false;
            }
        }

        return codeGenSucceeded;
    }

    /// <summary>
    /// Generates csv files describing the Lottie and its translation.
    /// </summary>
    bool TryGenerateStats(string outputFilePathBase)
    {
        if (!TryEnsureTranslated())
        {
            return false;
        }

        var issues = _readerIssues.Concat(_translationIssues);
        var translationStats = _afterOptimizationStats ?? _beforeOptimizationStats;

        // Assume success.
        var success = true;

        // Create an id for this file, based on the path.
        // The key is used so that other tables (e.g. the errors table) can reference the entry
        // for this file.
        var key = GenerateHashFromString(_file).Substring(0, 8);

        // Create the main table. Other tables will reference rows in this table.
        // Note that the main table has only one row. Typical usage will be to
        // generate tables for many Lottie files then combine them in a script.
        var sb = new StringBuilder();
        sb.AppendLine(
            "Key,Path,FileName,LottieVersion,DurationSeconds,ErrorCount,PrecompLayerCount,ShapeLayerCount," +
            "SolidLayerCount,ImageLayerCount,TextLayerCount,MaskCount,ContainerShapeCount,ContainerVisualCount," +
            "ExpressionAnimationCount,PropertySetPropertyCount,SpriteShapeCount");
        sb.Append($"\"{key}\"");
        AppendColumnValue(_file);
        AppendColumnValue(Path.GetFileName(_file));
        AppendColumnValue(_lottieStats.Version);
        AppendColumnValue(_lottieStats.Duration.TotalSeconds);
        AppendColumnValue(issues.Count());
        AppendColumnValue(_lottieStats.PreCompLayerCount);
        AppendColumnValue(_lottieStats.ShapeLayerCount);
        AppendColumnValue(_lottieStats.SolidLayerCount);
        AppendColumnValue(_lottieStats.ImageLayerCount);
        AppendColumnValue(_lottieStats.TextLayerCount);
        AppendColumnValue(_lottieStats.MaskCount);
        AppendColumnValue(translationStats.ContainerShapeCount);
        AppendColumnValue(translationStats.ContainerVisualCount);
        AppendColumnValue(translationStats.ExpressionAnimationCount);
        AppendColumnValue(translationStats.PropertySetPropertyCount);
        AppendColumnValue(translationStats.SpriteShapeCount);
        sb.AppendLine();

        WriteCsvFile("basicInfo", sb.ToString());

        // Create the error table.
        sb.Clear();
        sb.AppendLine("Key,ErrorCode,Description");
        foreach ((var code, var description) in issues)
        {
            sb.Append($"\"{key}\"");
            AppendColumnValue(code);
            AppendColumnValue(description);
            sb.AppendLine();
        }

        WriteCsvFile("errors", sb.ToString());

        void AppendColumnValue(object columnValue)
        {
            sb.Append($",\"{columnValue}\"");
        }

        void WriteCsvFile(string fileDifferentiator, string text)
        {
            var filePath = $"{outputFilePathBase}.{fileDifferentiator}.csv";

            success &= TryWriteTextFile(filePath, text);
            if (success)
            {
                _reporter.WriteInfo($"Stats data written to {filePath}");
            }
        }

        return success;
    }

    bool TryGenerateLottieXml(
        string outputFilePath)
    {
        var result = TryWriteTextFile(
            outputFilePath,
            LottieCompositionXmlSerializer.ToXml(_lottieComposition).ToString());

        _reporter.WriteInfo($"Lottie XML written to {outputFilePath}");

        return result;
    }

    bool TryGenerateLottieYaml(string outputFilePath)
    {
        var result = TryWriteTextFile(
            outputFilePath,
            writer => LottieCompositionYamlSerializer.WriteYaml(_lottieComposition, writer, _file));

        if (result)
        {
            _reporter.WriteInfo($"Lottie YAML written to {outputFilePath}");
        }

        return result;
    }

    bool TryGenerateWincompXml(
        string outputFilePath)
    {
        if (!TryEnsureTranslated())
        {
            return false;
        }

        var result = TryWriteTextFile(
            outputFilePath,
            CompositionObjectXmlSerializer.ToXml(_rootVisual).ToString());

        if (result)
        {
            _reporter.WriteInfo($"WinComp XML written to {outputFilePath}");
        }

        return result;
    }

    bool TryGenerateWincompDgml(string outputFilePath)
    {
        if (!TryEnsureTranslated())
        {
            return false;
        }

        var result = TryWriteTextFile(
            outputFilePath,
            CompositionObjectDgmlSerializer.ToXml(_rootVisual).ToString());

        if (result)
        {
            _reporter.WriteInfo($"WinComp DGML written to {outputFilePath}");
        }

        return result;
    }

    bool TryGenerateCSharpCode(string outputFilePath)
    {
        if (!TryEnsureTranslated())
        {
            return false;
        }

        var code = CSharpInstantiatorGenerator.CreateFactoryCode(
                    _className,
                    _rootVisual,
                    (float)_lottieComposition.Width,
                    (float)_lottieComposition.Height,
                    _lottieComposition.Duration,
                    _options.DisableCodeGenOptimizer);

        if (string.IsNullOrWhiteSpace(code))
        {
            _reporter.WriteError("Failed to create the C# code.");
            return false;
        }

        var result = TryWriteTextFile(outputFilePath, code);

        if (result)
        {
            _reporter.WriteInfo($"C# code for class {_className} written to {outputFilePath}");
        }

        return result;
    }

    bool TryGenerateCXCode(
        string outputHeaderFilePath,
        string outputCppFilePath)
    {
        if (!TryEnsureTranslated())
        {
            return false;
        }

        CxInstantiatorGenerator.CreateFactoryCode(
                _className,
                _rootVisual,
                (float)_lottieComposition.Width,
                (float)_lottieComposition.Height,
                _lottieComposition.Duration,
                Path.GetFileName(outputHeaderFilePath),
                out var cppText,
                out var hText,
                _options.DisableCodeGenOptimizer);

        if (string.IsNullOrWhiteSpace(cppText))
        {
            _reporter.WriteError("Failed to generate the .cpp code.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(hText))
        {
            _reporter.WriteError("Failed to generate the .h code.");
            return false;
        }

        if (!TryWriteTextFile(outputHeaderFilePath, hText))
        {
            return false;
        }

        if (!TryWriteTextFile(outputCppFilePath, cppText))
        {
            return false;
        }

        _reporter.WriteInfo($"Header code for class {_className} written to {outputHeaderFilePath}");
        _reporter.WriteInfo($"Source code for class {_className} written to {outputCppFilePath}");
        return true;
    }

    bool TryWriteTextFile(string filePath, Action<StreamWriter> writer)
    {
        try
        {
            using (var stream = File.Open(filePath, FileMode.Create, FileAccess.Write))
            using (var streamWriter = new StreamWriter(stream))
            {
                writer(streamWriter);
            }

            return true;
        }
        catch (Exception e)
        {
            _reporter.WriteError($"Failed to write to {filePath}");
            _reporter.WriteError(e.Message);
            return false;
        }
    }

    bool TryWriteTextFile(string filePath, string contents)
    {
        try
        {
            File.WriteAllText(filePath, contents, Encoding.UTF8);
            return true;
        }
        catch (Exception e)
        {
            _reporter.WriteError($"Failed to write to {filePath}");
            _reporter.WriteError(e.Message);
            return false;
        }
    }

    static bool TryEnsureDirectoryExists(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    Stream TryReadTextFile(string filePath)
    {
        _reporter.WriteInfo($"Reading file: {_file}");

        try
        {
            return File.OpenRead(filePath);
        }
        catch (Exception e)
        {
            _reporter.WriteError($"Failed to read from {filePath}");
            _reporter.WriteError(e.Message);
            return null;
        }
    }

    // Outputs an error or warning message describing the error with the file path, error code, and description.
    // The format is designed to be suitable for parsing by VS.
    string IssueToString(string originatingFile, (string Code, string Description) issue)
    {
        if (_options.StrictMode)
        {
            _reportedErrors = true;
            return ErrorToString(originatingFile, issue);
        }
        else
        {
            return WarningToString(originatingFile, issue);
        }
    }

    // Outputs an error message describing the error with the file path, error code, and description.
    // The format is designed to be suitable for parsing by VS.
    static string ErrorToString(string originatingFile, (string Code, string Description) issue)
        => ErrorOrWarningToString(originatingFile, issue, "error");

    // Outputs a warning message describing a warning with the file path, error code, and description.
    // The format is designed to be suitable for parsing by VS.
    static string WarningToString(string originatingFile, (string Code, string Description) issue)
        => ErrorOrWarningToString(originatingFile, issue, "warning");

    static string ErrorOrWarningToString(string originatingFile, (string Code, string Description) issue, string errorOrWarning)
    {
        return $"{originatingFile}: {errorOrWarning} {issue.Code}: {issue.Description}";
    }

    static string GenerateHashFromString(string input)
    {
        using (var hasher = SHA256.Create())
        using (var stream = new MemoryStream())
        using (var writer = new StreamWriter(stream))
        {
            // Write into the stream so that hasher can consume the input.
            writer.Write(input);

            // Generate the hash. This returns a 32 byte (256 bit) value.
            var hash = hasher.ComputeHash(stream);

            // Encode the hash as base 64 so that it is all readable characters.
            // This will return a string that is 44 characters long.
            var hashedString = Convert.ToBase64String(hash);

            // Return just the first 8 characters of the base64 encoded hash.
            return hashedString.Substring(0, 8);
        }
    }

    static void WriteCodeGenStatsReport(
        TextWriter writer,
        Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats beforeOptimization,
        Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats afterOptimization)
    {
        if (beforeOptimization == null)
        {
            return;
        }

        writer.WriteLine(" === Translation output stats ===");

        writer.WriteLine("                      Type   Count  Optimized away");

        if (afterOptimization == null)
        {
            // No optimization was performed. Just report on the before stats.
            afterOptimization = beforeOptimization;
        }

        // Report on the after stats and indicate how much optimization
        // improved things (where it did).
        WriteStatsLine("CanvasGeometry", beforeOptimization.CanvasGeometryCount, afterOptimization.CanvasGeometryCount);
        WriteStatsLine("ColorBrush", beforeOptimization.ColorBrushCount, afterOptimization.ColorBrushCount);
        WriteStatsLine("ColorKeyFrameAnimation", beforeOptimization.ColorKeyFrameAnimationCount, afterOptimization.ColorKeyFrameAnimationCount);
        WriteStatsLine("CompositionPath", beforeOptimization.CompositionPathCount, afterOptimization.CompositionPathCount);
        WriteStatsLine("ContainerShape", beforeOptimization.ContainerShapeCount, afterOptimization.ContainerShapeCount);
        WriteStatsLine("ContainerVisual", beforeOptimization.ContainerVisualCount, afterOptimization.ContainerVisualCount);
        WriteStatsLine("CubicBezierEasingFunction", beforeOptimization.CubicBezierEasingFunctionCount, afterOptimization.CubicBezierEasingFunctionCount);
        WriteStatsLine("EllipseGeometry", beforeOptimization.EllipseGeometryCount, afterOptimization.EllipseGeometryCount);
        WriteStatsLine("ExpressionAnimation", beforeOptimization.ExpressionAnimationCount, afterOptimization.ExpressionAnimationCount);
        WriteStatsLine("GeometricClip", beforeOptimization.GeometricClipCount, afterOptimization.GeometricClipCount);
        WriteStatsLine("InsetClip", beforeOptimization.InsetClipCount, afterOptimization.InsetClipCount);
        WriteStatsLine("LinearEasingFunction", beforeOptimization.LinearEasingFunctionCount, afterOptimization.LinearEasingFunctionCount);
        WriteStatsLine("PathGeometry", beforeOptimization.PathGeometryCount, afterOptimization.PathGeometryCount);
        WriteStatsLine("PathKeyFrameAnimation", beforeOptimization.PathKeyFrameAnimationCount, afterOptimization.PathKeyFrameAnimationCount);
        WriteStatsLine("Property value", beforeOptimization.PropertySetPropertyCount, afterOptimization.PropertySetPropertyCount);
        WriteStatsLine("PropertySet", beforeOptimization.PropertySetCount, afterOptimization.PropertySetCount);
        WriteStatsLine("RectangleGeometry", beforeOptimization.RectangleGeometryCount, afterOptimization.RectangleGeometryCount);
        WriteStatsLine("RoundedRectangleGeometry", beforeOptimization.RoundedRectangleGeometryCount, afterOptimization.RoundedRectangleGeometryCount);
        WriteStatsLine("ScalarKeyFrameAnimation", beforeOptimization.ScalarKeyFrameAnimationCount, afterOptimization.ScalarKeyFrameAnimationCount);
        WriteStatsLine("ShapeVisual", beforeOptimization.ShapeVisualCount, afterOptimization.ShapeVisualCount);
        WriteStatsLine("SpriteShape", beforeOptimization.SpriteShapeCount, afterOptimization.SpriteShapeCount);
        WriteStatsLine("StepEasingFunction", beforeOptimization.StepEasingFunctionCount, afterOptimization.StepEasingFunctionCount);
        WriteStatsLine("Vector2KeyFrameAnimation", beforeOptimization.Vector2KeyFrameAnimationCount, afterOptimization.Vector2KeyFrameAnimationCount);
        WriteStatsLine("Vector3KeyFrameAnimation", beforeOptimization.Vector3KeyFrameAnimationCount, afterOptimization.Vector3KeyFrameAnimationCount);
        WriteStatsLine("ViewBox", beforeOptimization.ViewBoxCount, afterOptimization.ViewBoxCount);

        void WriteStatsLine(string name, int before, int after)
        {
            if (after > 0 || before > 0)
            {
                const int nameWidth = 26;
                if (before != after)
                {
                    writer.WriteLine($"{name,nameWidth}  {after,6:n0} {before - after,6:n0}");
                }
                else
                {
                    writer.WriteLine($"{name,nameWidth}  {after,6:n0}");
                }
            }
        }
    }

    static void WriteLottieStatsReport(
        TextWriter writer,
        Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats stats)
    {
        writer.WriteLine(" === Lottie info ===");
        WriteStatsStringLine("BodyMovin Version", stats.Version.ToString());
        WriteStatsStringLine("Name", stats.Name);
        WriteStatsStringLine("Size", $"{stats.Width} x {stats.Height}");
        WriteStatsStringLine("Duration", $"{stats.Duration.TotalSeconds.ToString("#,##0.0##")} seconds");
        WriteStatsIntLine("Images", stats.ImageLayerCount);
        WriteStatsIntLine("PreComps", stats.PreCompLayerCount);
        WriteStatsIntLine("Shapes", stats.ShapeLayerCount);
        WriteStatsIntLine("Solids", stats.SolidLayerCount);
        WriteStatsIntLine("Nulls", stats.NullLayerCount);
        WriteStatsIntLine("Texts", stats.TextLayerCount);
        WriteStatsIntLine("Masks", stats.MaskCount);
        WriteStatsIntLine("MaskAdditive", stats.MaskAdditiveCount);
        WriteStatsIntLine("MaskDarken", stats.MaskDarkenCount);
        WriteStatsIntLine("MaskDifference", stats.MaskDifferenceCount);
        WriteStatsIntLine("MaskIntersect", stats.MaskIntersectCount);
        WriteStatsIntLine("MaskLighten", stats.MaskLightenCount);
        WriteStatsIntLine("MaskSubtract", stats.MaskSubtractCount);

        const int nameWidth = 19;
        void WriteStatsIntLine(string name, int value)
        {
            if (value > 0)
            {
                writer.WriteLine($"{name,nameWidth}  {value,6:n0}");
            }
        }

        void WriteStatsStringLine(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                writer.WriteLine($"{name,nameWidth}  {value}");
            }
        }
    }

    bool TryEnsureTranslated()
    {
        if (_isTranslatedSuccessfully.HasValue)
        {
            return _isTranslatedSuccessfully.Value;
        }

        var translateSucceeded = LottieToWinCompTranslator.TryTranslateLottieComposition(
               _lottieComposition,
               strictTranslation: false,
               addCodegenDescriptions: true,
               out _rootVisual,
               out _translationIssues);

        _profiler.OnTranslateFinished();

        foreach (var issue in _translationIssues)
        {
            _reporter.WriteInfo(IssueToString(_file, issue));
        }

        _beforeOptimizationStats = new Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats(_rootVisual);
        _profiler.OnUnmeasuredFinished();

        if (!translateSucceeded)
        {
            _isTranslatedSuccessfully = false;
            return false;
        }

        // Optimize the code unless told not to.
        if (!_options.DisableTranslationOptimizer)
        {
            _rootVisual = Optimizer.Optimize(_rootVisual, ignoreCommentProperties: true);
            _profiler.OnOptimizationFinished();

            _afterOptimizationStats = new Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats(_rootVisual);
            _profiler.OnUnmeasuredFinished();
        }

        _isTranslatedSuccessfully = true;
        return true;
    }
}
