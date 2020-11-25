// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Cppwinrt;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.CSharp;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Cx;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

/// <summary>
/// Processes a single Lottie .json file to produce various generated outputs.
/// </summary>
sealed class LottieJsonFileProcessor
{
    readonly CommandLineOptions _options;
    readonly Profiler _profiler = new Profiler();
    readonly Reporter _reporter;
    readonly string _sourceFilePath;
    readonly string _jsonFilePath;
    readonly string _outputFolder;
    readonly DateTime _timestamp;
    readonly uint _minimumUapVersion;
    readonly string _className;
    bool _reportedErrors;
    bool? _isTranslatedSuccessfully;
    Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats? _lottieStats;
    IReadOnlyList<(string Code, string Description)>? _readerIssues;
    Stats? _beforeOptimizationStats;
    Stats? _afterOptimizationStats;
    IReadOnlyList<TranslationResult>? _translationResults;
    IReadOnlyList<(TranslationIssue issue, UapVersionRange versionRange)>? _translationIssues;

    LottieJsonFileProcessor(
        CommandLineOptions options,
        Reporter reporter,
        string sourceFilePath,
        string jsonFilePath,
        string className,
        string outputFolder,
        DateTime timestamp)
    {
        _options = options;
        _reporter = reporter;
        _sourceFilePath = sourceFilePath;
        _jsonFilePath = jsonFilePath;
        _className = className;
        _outputFolder = outputFolder;
        _timestamp = timestamp;

        // If no minimum UAP version is specified, use 7 as that is the lowest version that the translator supports.
        const uint defaultUapVersion = 7;

        _minimumUapVersion = _options.MinimumUapVersion ?? defaultUapVersion;
    }

    /// <summary>
    /// Processes a single .json file containing a Lottie animation.
    /// </summary>
    /// <param name="options">The kinds of processing to perform.</param>
    /// <param name="reporter">Receives progress and status information.</param>
    /// <param name="sourceFilePath">The file in which the Lottie animation is contained.</param>
    /// <param name="jsonFilePath">A path describing where the .json file is.</param>
    /// <param name="className">The name to give to the generated C# and/or C++ class.</param>
    /// <param name="jsonStream">The stream of bytes containined in the .json file.</param>
    /// <param name="outputFolder">Where the output should be written.</param>
    /// <param name="timestamp">A timestamp used to indicate when the processing took place.</param>
    /// <returns><c>true</c> if the processing succeeded.</returns>
    internal static bool ProcessLottieJsonFile(
        CommandLineOptions options,
        Reporter reporter,
        string sourceFilePath,
        string jsonFilePath,
        string className,
        Stream jsonStream,
        string outputFolder,
        DateTime timestamp) =>
            new LottieJsonFileProcessor(
                options: options,
                reporter: reporter,
                sourceFilePath: sourceFilePath,
                jsonFilePath: jsonFilePath,
                className: className,
                outputFolder: outputFolder,
                timestamp: timestamp).Run(jsonStream);

    bool Run(Stream jsonStream)
    {
        // Make sure we can write to the output directory.
        if (!TryEnsureDirectoryExists(_outputFolder))
        {
            _reporter.WriteError($"Failed to create the output directory: {_outputFolder}");
            return false;
        }

        // Parse the Lottie.
        var lottieComposition =
            LottieCompositionReader.ReadLottieCompositionFromJsonStream(
                jsonStream,
                LottieCompositionReader.Options.IgnoreMatchNames,
                out _readerIssues);

        _profiler.OnParseFinished();

        foreach (var issue in _readerIssues)
        {
            _reporter.WriteInfo(InfoType.Issue, IssueToString(_jsonFilePath, issue));
        }

        if (lottieComposition is null)
        {
            _reporter.WriteError($"Failed to parse Lottie file: {_jsonFilePath}");
            return false;
        }

        // Validate the Lottie.
        foreach (var issue in LottieCompositionValidator.Validate(lottieComposition))
        {
            _reporter.WriteInfo(InfoType.Issue, IssueToString(_jsonFilePath, issue));
        }

        _lottieStats = new Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats(lottieComposition);

        var codeGenResult = TryGenerateCode(lottieComposition);

        // Any error that was reported is treated as a failure.
        codeGenResult &= !_reportedErrors;

        return codeGenResult;
    }

    bool TryGenerateCode(LottieComposition lottieComposition)
    {
        var outputFileBase = System.IO.Path.Combine(_outputFolder, _className);

        var codeGenSucceeded = true;

        var areBothCppwinrtAndCxRequested = _options.Languages.Where(l => l == Lang.Cppwinrt || l == Lang.Cx).Count() == 2;

        foreach (var lang in _options.Languages)
        {
            switch (lang)
            {
                case Lang.CSharp:
                    codeGenSucceeded &= TryGenerateCSharpCode(lottieComposition, $"{outputFileBase}.cs");
                    _profiler.OnCodeGenFinished();
                    break;

                case Lang.Cx:
                    {
                        // If both cppwinrt and cx files were requested, add a differentiator to
                        // the folder name for the cx files to make their names distinct from the
                        // cppwinrt output. This ensures the cx doesn't overwrite the cppwinrt.
                        var cxDifferentiator = areBothCppwinrtAndCxRequested ? "CX" : string.Empty;
                        var outputFolder = $"{_outputFolder}\\{cxDifferentiator}";
                        var directoryExists = TryEnsureDirectoryExists(outputFolder);
                        if (!directoryExists)
                        {
                            codeGenSucceeded = false;
                        }
                        else
                        {
                            codeGenSucceeded &= TryGenerateCXCode(lottieComposition, outputFolder);
                        }

                        _profiler.OnCodeGenFinished();
                        break;
                    }

                case Lang.Cppwinrt:
                    codeGenSucceeded &= TryGenerateCppwinrtCode(lottieComposition, _outputFolder);
                    _profiler.OnCodeGenFinished();
                    break;

                case Lang.LottieYaml:
                    codeGenSucceeded &= TryGenerateLottieYaml(lottieComposition, $"{outputFileBase}-Lottie.yaml");
                    _profiler.OnSerializationFinished();
                    break;

                case Lang.WinCompDgml:
                    codeGenSucceeded &= TryGenerateWincompDgml(lottieComposition, $"{outputFileBase}.dgml");
                    _profiler.OnSerializationFinished();
                    break;

                case Lang.Stats:
                    codeGenSucceeded &= TryGenerateStats(lottieComposition);
                    break;

                default:
                    _reporter.WriteError($"Language {lang} is not supported.");
                    return false;
            }
        }

        return codeGenSucceeded;
    }

    /// <summary>
    /// Generates data describing the Lottie and its translation.
    /// </summary>
    bool TryGenerateStats(LottieComposition lottieComposition)
    {
        if (!TryEnsureTranslated(lottieComposition))
        {
            return false;
        }

        // Write the profiler table.
        _reporter.WriteDataTableRow(
            "Timing",
            new[]
            {
                ("Path", _jsonFilePath),
                ("TotalSeconds", (_profiler.ParseTime +
                                  _profiler.TranslateTime +
                                  _profiler.OptimizationTime +
                                  _profiler.CodegenTime +
                                  _profiler.SerializationTime).TotalSeconds.ToString()),
                ("ParseSeconds", _profiler.ParseTime.TotalSeconds.ToString()),
                ("TranslateSeconds", _profiler.TranslateTime.TotalSeconds.ToString()),
                ("OptimizationSeconds", _profiler.OptimizationTime.TotalSeconds.ToString()),
                ("CodegenSeconds", _profiler.CodegenTime.TotalSeconds.ToString()),
                ("SerializationSeconds", _profiler.SerializationTime.TotalSeconds.ToString()),
            });

        if (_lottieStats is null)
        {
            throw new InvalidOperationException();
        }

        // Write the Lottie stats table.
        _reporter.WriteDataTableRow(
            "Lottie",
            new[]
            {
                    ("Path", _jsonFilePath),
                    ("Name", _lottieStats.Name),
                    ("BodyMovinVersion", _lottieStats.Version.ToString()),
                    ("DurationSeconds", _lottieStats.Duration.TotalSeconds.ToString()),
                    ("Width", _lottieStats.Width.ToString()),
                    ("Height", _lottieStats.Height.ToString()),
                    ("Error", _readerIssues.Count().ToString()),
                    ("ImageLayer", _lottieStats.ImageLayerCount.ToString()),
                    ("NullLayer", _lottieStats.NullLayerCount.ToString()),
                    ("PrecompLayer", _lottieStats.PreCompLayerCount.ToString()),
                    ("ShapeLayer", _lottieStats.ShapeLayerCount.ToString()),
                    ("SolidLayer", _lottieStats.SolidLayerCount.ToString()),
                    ("TextLayer", _lottieStats.TextLayerCount.ToString()),
                    ("Mask", _lottieStats.MaskCount.ToString()),
                    ("LinearGradientFill", _lottieStats.LinearGradientFillCount.ToString()),
                    ("LinearGradientStroke", _lottieStats.LinearGradientStrokeCount.ToString()),
                    ("RadialGradientFill", _lottieStats.RadialGradientFillCount.ToString()),
                    ("RadialGradientStroke", _lottieStats.RadialGradientStrokeCount.ToString()),
            });

        var translationStats = _afterOptimizationStats ?? _beforeOptimizationStats;

        if (translationStats is null)
        {
            throw new InvalidOperationException();
        }

        // Write the WinComp stats table.
        _reporter.WriteDataTableRow(
            "WinComp",
            new[]
            {
                    ("Path", _jsonFilePath),
                    ("Animator", translationStats.AnimatorCount.ToString()),
                    ("BooleanKeyFrameAnimation", translationStats.BooleanKeyFrameAnimationCount.ToString()),
                    ("CanvasGeometry", translationStats.CanvasGeometryCount.ToString()),
                    ("ColorBrush", translationStats.ColorBrushCount.ToString()),
                    ("ColorGradientStop", translationStats.ColorGradientStopCount.ToString()),
                    ("ColorKeyFrameAnimation", translationStats.ColorKeyFrameAnimationCount.ToString()),
                    ("CompositionPath", translationStats.CompositionPathCount.ToString()),
                    ("ContainerShape", translationStats.ContainerShapeCount.ToString()),
                    ("ContainerVisual", translationStats.ContainerVisualCount.ToString()),
                    ("CubicBezierEasingFunction", translationStats.CubicBezierEasingFunctionCount.ToString()),
                    ("EffectBrush", translationStats.EffectBrushCount.ToString()),
                    ("EllipseGeometry", translationStats.EllipseGeometryCount.ToString()),
                    ("ExpressionAnimation", translationStats.ExpressionAnimationCount.ToString()),
                    ("GeometricClip", translationStats.GeometricClipCount.ToString()),
                    ("InsetClip", translationStats.InsetClipCount.ToString()),
                    ("LayerVisual", translationStats.LayerVisualCount.ToString()),
                    ("LinearEasingFunction", translationStats.LinearEasingFunctionCount.ToString()),
                    ("LinearGradientBrush", translationStats.LinearGradientBrushCount.ToString()),
                    ("PathGeometry", translationStats.PathGeometryCount.ToString()),
                    ("PathKeyFrameAnimation", translationStats.PathKeyFrameAnimationCount.ToString()),
                    ("Property value", translationStats.PropertySetPropertyCount.ToString()),
                    ("PropertySet", translationStats.PropertySetCount.ToString()),
                    ("RadialGradientBrush", translationStats.RadialGradientBrushCount.ToString()),
                    ("RectangleGeometry", translationStats.RectangleGeometryCount.ToString()),
                    ("RoundedRectangleGeometry", translationStats.RoundedRectangleGeometryCount.ToString()),
                    ("ScalarKeyFrameAnimation", translationStats.ScalarKeyFrameAnimationCount.ToString()),
                    ("ShapeVisual", translationStats.ShapeVisualCount.ToString()),
                    ("SpriteShape", translationStats.SpriteShapeCount.ToString()),
                    ("SpriteVisualCount", translationStats.SpriteVisualCount.ToString()),
                    ("StepEasingFunction", translationStats.StepEasingFunctionCount.ToString()),
                    ("SurfaceBrushCount", translationStats.SurfaceBrushCount.ToString()),
                    ("Vector2KeyFrameAnimation", translationStats.Vector2KeyFrameAnimationCount.ToString()),
                    ("Vector3KeyFrameAnimation", translationStats.Vector3KeyFrameAnimationCount.ToString()),
                    ("Vector4KeyFrameAnimation", translationStats.Vector4KeyFrameAnimationCount.ToString()),
                    ("ViewBox", translationStats.ViewBoxCount.ToString()),
                    ("VisualSurfaceCount", translationStats.VisualSurfaceCount.ToString()),
            });

        if (_beforeOptimizationStats is null)
        {
            throw new InvalidOperationException();
        }

        // Write the WinComp optimization stats table.
        if (_afterOptimizationStats != null)
        {
            _reporter.WriteDataTableRow(
                "WinCompOptimization",
                new[]
                {
                    ("Path", _jsonFilePath),
                    ("Animator", (_afterOptimizationStats.AnimatorCount - _beforeOptimizationStats.AnimatorCount).ToString()),
                    ("BooleanKeyFrameAnimation", (_afterOptimizationStats.BooleanKeyFrameAnimationCount - _beforeOptimizationStats.BooleanKeyFrameAnimationCount).ToString()),
                    ("CanvasGeometry", (_afterOptimizationStats.CanvasGeometryCount - _beforeOptimizationStats.CanvasGeometryCount).ToString()),
                    ("ColorBrush", (_afterOptimizationStats.ColorBrushCount - _beforeOptimizationStats.ColorBrushCount).ToString()),
                    ("ColorGradientStop", (_afterOptimizationStats.ColorGradientStopCount - _beforeOptimizationStats.ColorGradientStopCount).ToString()),
                    ("ColorKeyFrameAnimation", (_afterOptimizationStats.ColorKeyFrameAnimationCount - _beforeOptimizationStats.ColorKeyFrameAnimationCount).ToString()),
                    ("CompositionPath", (_afterOptimizationStats.CompositionPathCount - _beforeOptimizationStats.CompositionPathCount).ToString()),
                    ("ContainerShape", (_afterOptimizationStats.ContainerShapeCount - _beforeOptimizationStats.ContainerShapeCount).ToString()),
                    ("ContainerVisual", (_afterOptimizationStats.ContainerVisualCount - _beforeOptimizationStats.ContainerVisualCount).ToString()),
                    ("CubicBezierEasingFunction", (_afterOptimizationStats.CubicBezierEasingFunctionCount - _beforeOptimizationStats.CubicBezierEasingFunctionCount).ToString()),
                    ("EffectBrush", (_afterOptimizationStats.EffectBrushCount - _beforeOptimizationStats.EffectBrushCount).ToString()),
                    ("EllipseGeometry", (_afterOptimizationStats.EllipseGeometryCount - _beforeOptimizationStats.EllipseGeometryCount).ToString()),
                    ("ExpressionAnimation", (_afterOptimizationStats.ExpressionAnimationCount - _beforeOptimizationStats.ExpressionAnimationCount).ToString()),
                    ("GeometricClip", (_afterOptimizationStats.GeometricClipCount - _beforeOptimizationStats.GeometricClipCount).ToString()),
                    ("InsetClip", (_afterOptimizationStats.InsetClipCount - _beforeOptimizationStats.InsetClipCount).ToString()),
                    ("LinearEasingFunction", (_afterOptimizationStats.LinearEasingFunctionCount - _beforeOptimizationStats.LinearEasingFunctionCount).ToString()),
                    ("LinearGradientBrush", (_afterOptimizationStats.LinearGradientBrushCount - _beforeOptimizationStats.LinearGradientBrushCount).ToString()),
                    ("PathGeometry", (_afterOptimizationStats.PathGeometryCount - _beforeOptimizationStats.PathGeometryCount).ToString()),
                    ("PathKeyFrameAnimation", (_afterOptimizationStats.PathKeyFrameAnimationCount - _beforeOptimizationStats.PathKeyFrameAnimationCount).ToString()),
                    ("Property value", (_afterOptimizationStats.PropertySetPropertyCount - _beforeOptimizationStats.PropertySetPropertyCount).ToString()),
                    ("PropertySet", (_afterOptimizationStats.PropertySetCount - _beforeOptimizationStats.PropertySetCount).ToString()),
                    ("RadialGradientBrush", (_afterOptimizationStats.RadialGradientBrushCount - _beforeOptimizationStats.RadialGradientBrushCount).ToString()),
                    ("RectangleGeometry", (_afterOptimizationStats.RectangleGeometryCount - _beforeOptimizationStats.RectangleGeometryCount).ToString()),
                    ("RoundedRectangleGeometry", (_afterOptimizationStats.RoundedRectangleGeometryCount - _beforeOptimizationStats.RoundedRectangleGeometryCount).ToString()),
                    ("ScalarKeyFrameAnimation", (_afterOptimizationStats.ScalarKeyFrameAnimationCount - _beforeOptimizationStats.ScalarKeyFrameAnimationCount).ToString()),
                    ("ShapeVisual", (_afterOptimizationStats.ShapeVisualCount - _beforeOptimizationStats.ShapeVisualCount).ToString()),
                    ("SpriteShape", (_afterOptimizationStats.SpriteShapeCount - _beforeOptimizationStats.SpriteShapeCount).ToString()),
                    ("SpriteVisualCount", (_afterOptimizationStats.SpriteVisualCount - _beforeOptimizationStats.SpriteVisualCount).ToString()),
                    ("StepEasingFunction", (_afterOptimizationStats.StepEasingFunctionCount - _beforeOptimizationStats.StepEasingFunctionCount).ToString()),
                    ("SurfaceBrushCount", (_afterOptimizationStats.SurfaceBrushCount - _beforeOptimizationStats.SurfaceBrushCount).ToString()),
                    ("Vector2KeyFrameAnimation", (_afterOptimizationStats.Vector2KeyFrameAnimationCount - _beforeOptimizationStats.Vector2KeyFrameAnimationCount).ToString()),
                    ("Vector3KeyFrameAnimation", (_afterOptimizationStats.Vector3KeyFrameAnimationCount - _beforeOptimizationStats.Vector3KeyFrameAnimationCount).ToString()),
                    ("Vector4KeyFrameAnimation", (_afterOptimizationStats.Vector4KeyFrameAnimationCount - _beforeOptimizationStats.Vector4KeyFrameAnimationCount).ToString()),
                    ("ViewBox", (_afterOptimizationStats.ViewBoxCount - _beforeOptimizationStats.ViewBoxCount).ToString()),
                    ("VisualSurfaceCount", (_afterOptimizationStats.VisualSurfaceCount - _beforeOptimizationStats.VisualSurfaceCount).ToString()),
                });
        }

        // Write the error table.
        var issues = _readerIssues.Concat(_translationIssues.Select(i => (Code: i.issue.Code, Description: i.issue.Description)));

        foreach (var (code, description) in issues)
        {
            _reporter.WriteDataTableRow(
                "Errors",
                new[]
                {
                    ("Path", _jsonFilePath),
                    ("ErrorCode", code),
                    ("Description", description),
                });
        }

        return true;
    }

    bool TryGenerateLottieYaml(LottieComposition lottieComposition, string outputFilePath)
    {
        var result = TryWriteTextFile(
            outputFilePath,
            writer => LottieCompositionYamlSerializer.WriteYaml(
                            lottieComposition,
                            writer,
                            comment: System.IO.Path.GetFileName(_jsonFilePath)));

        if (result)
        {
            using (_reporter.InfoStream.Lock())
            {
                _reporter.WriteInfo("Lottie YAML written to:");
                _reporter.WriteInfo(InfoType.FilePath, $" {outputFilePath}");
            }
        }

        return result;
    }

    bool TryGenerateWincompDgml(
        LottieComposition lottieComposition,
        string outputFilePath)
    {
        if (!TryEnsureTranslated(lottieComposition))
        {
            return false;
        }

        if (_translationResults is null)
        {
            throw new InvalidOperationException();
        }

        // NOTE: this only writes the latest version of a multi-version translation.
        var result = TryWriteTextFile(
            outputFilePath,
            CompositionObjectDgmlSerializer.ToXml(_translationResults[0].RootVisual!).ToString());

        if (result)
        {
            using (_reporter.InfoStream.Lock())
            {
                _reporter.WriteInfo("WinComp DGML written to:");
                _reporter.WriteInfo(InfoType.FilePath, $" {outputFilePath}");
            }
        }

        return result;
    }

    bool TryGenerateCSharpCode(LottieComposition lottieComposition, string outputFilePath)
    {
        if (!TryEnsureTranslated(lottieComposition))
        {
            return false;
        }

        var codegenResult =
            CSharpInstantiatorGenerator.CreateFactoryCode(CreateCodeGenConfiguration(lottieComposition, "CSharp"));

        if (string.IsNullOrWhiteSpace(codegenResult.CsText))
        {
            _reporter.WriteError("Failed to create the C# code.");
            return false;
        }

        var result = TryWriteTextFile(outputFilePath, codegenResult.CsText);

        if (result)
        {
            using (_reporter.InfoStream.Lock())
            {
                _reporter.WriteInfo($"C# code for class {_className} written to:");
                _reporter.WriteInfo(InfoType.FilePath, $" {outputFilePath}");
            }

            if (codegenResult.Assets != null)
            {
                // Write out the list of asset files referenced by the code.
                WriteAssetFiles(codegenResult.Assets);
            }
        }

        return result;
    }

    bool TryGenerateCppwinrtCode(LottieComposition lottieComposition, string outputFolder)
    {
        if (!TryEnsureTranslated(lottieComposition))
        {
            return false;
        }

        var codegenResult =
                    CppwinrtInstantiatorGenerator.CreateFactoryCode(CreateCodeGenConfiguration(lottieComposition, "Cppwinrt"));

        if (string.IsNullOrWhiteSpace(codegenResult.CppText))
        {
            _reporter.WriteError("Failed to generate the .cpp code.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(codegenResult.HText))
        {
            _reporter.WriteError("Failed to generate the .h code.");
            return false;
        }

        var cppFilePath = System.IO.Path.Combine(outputFolder, codegenResult.CppFilename);
        var hFilePath = System.IO.Path.Combine(outputFolder, codegenResult.HFilename);
        var idlFilePath = System.IO.Path.Combine(outputFolder, codegenResult.IdlFilename);

        if (!(TryWriteTextFile(hFilePath, codegenResult.HText) &&
            TryWriteTextFile(cppFilePath, codegenResult.CppText) &&
            TryWriteTextFile(idlFilePath, codegenResult.IdlText)))
        {
            return false;
        }

        using (_reporter.InfoStream.Lock())
        {
            _reporter.WriteInfo($"Cppwinrt header for class {_className} written to:");
            _reporter.WriteInfo(InfoType.FilePath, $" {hFilePath}");

            _reporter.WriteInfo($"Cppwinrt source for class {_className} written to:");
            _reporter.WriteInfo(InfoType.FilePath, $" {cppFilePath}");

            _reporter.WriteInfo($"Cppwinrt IDL for class {_className} written to:");
            _reporter.WriteInfo(InfoType.FilePath, $" {idlFilePath}");

            if (codegenResult.Assets != null)
            {
                // Write out the list of asset files referenced by the code.
                WriteAssetFiles(codegenResult.Assets);
            }
        }

        return true;
    }

    bool TryGenerateCXCode(LottieComposition lottieComposition, string outputFolder)
    {
        if (!TryEnsureTranslated(lottieComposition))
        {
            return false;
        }

        var codegenResult =
            CxInstantiatorGenerator.CreateFactoryCode(CreateCodeGenConfiguration(lottieComposition, "CX"));

        if (string.IsNullOrWhiteSpace(codegenResult.CppText))
        {
            _reporter.WriteError("Failed to generate the .cpp code.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(codegenResult.HText))
        {
            _reporter.WriteError("Failed to generate the .h code.");
            return false;
        }

        var cppFilePath = System.IO.Path.Combine(outputFolder, codegenResult.CppFilename);
        var hFilePath = System.IO.Path.Combine(outputFolder, codegenResult.HFilename);

        if (!(TryWriteTextFile(hFilePath, codegenResult.HText) &&
              TryWriteTextFile(cppFilePath, codegenResult.CppText)))
        {
            return false;
        }

        using (_reporter.InfoStream.Lock())
        {
            _reporter.WriteInfo($"CX header for class {_className} written to:");
            _reporter.WriteInfo(InfoType.FilePath, $" {hFilePath}");

            _reporter.WriteInfo($"CX source for class {_className} written to:");
            _reporter.WriteInfo(InfoType.FilePath, $" {cppFilePath}");

            if (codegenResult.Assets != null)
            {
                // Write out the list of asset files referenced by the code.
                WriteAssetFiles(codegenResult.Assets);
            }
        }

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
            using (_reporter.ErrorStream.Lock())
            {
                _reporter.WriteError($"Failed to write to {filePath}");
                _reporter.WriteError(e.Message);
            }

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
            using (_reporter.ErrorStream.Lock())
            {
                _reporter.WriteError($"Failed to write to {filePath}");
                _reporter.WriteError(e.Message);
            }

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

    CodegenConfiguration CreateCodeGenConfiguration(
        LottieComposition lottieComposition,
        string languageSwitch)
    {
        if (_translationResults is null)
        {
            throw new InvalidOperationException();
        }

        var result = new CodegenConfiguration(
            className: _className,
            interfaceType: _options.InterfaceBaseName,
            additionalInterfaces: _options.AdditionalInterfaces,
            objectGraphs: _translationResults.Select(
                            tr => ((CompositionObject?)tr.RootVisual!, tr.MinimumRequiredUapVersion)).ToArray(),
            nameSpace: string.IsNullOrWhiteSpace(_options.Namespace)
                            ? "AnimatedVisuals"
                            : NormalizeNamespace(_options.Namespace),
            rootNamespace: string.IsNullOrEmpty(_options.RootNamespace)
                            ? string.Empty
                            : NormalizeNamespace(_options.RootNamespace),
            sourceMetadata: _translationResults[0].SourceMetadata,
            toolInfo: GetToolInvocationInfo(languageSwitch).ToArray(),
            winUIVersion: _options.WinUIVersion
            )
        {
            DisableOptimization = _options.DisableCodeGenOptimizer,
            Duration = lottieComposition.Duration,
            GenerateDependencyObject = _options.GenerateDependencyObject,
            Height = lottieComposition.Height,
            Public = _options.Public,
            Width = lottieComposition.Width,
            WinUIVersion = _options.WinUIVersion,
        };

        return result;
    }

    // Returns lines that describe the invocation of this tool.
    // This information is passed to the code generator so that it can
    // be included in the generated output.
    IEnumerable<string> GetToolInvocationInfo(string languageSwitch)
    {
        var inputFile = new FileInfo(_sourceFilePath);

        var indent = "    ";

        if (!_options.TestMode)
        {
            yield return $"{ThisAssembly.AssemblyName} version:";
            yield return $"{indent}{ThisAssembly.AssemblyInformationalVersion}";
            yield return string.Empty;
        }

        var syntheticCommandLine =
            $"{_options.ToConfigurationCommandLine(languageSwitch)} -InputFile {inputFile.Name}";

        yield return "Command:";
        yield return $"{indent}{syntheticCommandLine}";

        yield return string.Empty;
        yield return "Input file:";
        yield return $"{indent}{inputFile.Name} ({inputFile.Length:g} bytes created {inputFile.CreationTimeUtc.ToLocalTime():H:mmK MMM d yyyy})";

        yield return string.Empty;
        yield return $"{ThisAssembly.AssemblyName} source:";
        yield return $"{indent}http://aka.ms/Lottie";
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

    string IssueToString(string originatingFile, TranslationIssue issue, UapVersionRange uapVersionRange)
        => IssueToString(
            originatingFile,
            (Code: issue.Code, Description: $"{issue.Description}{CreateUapVersionRangeQualifier(uapVersionRange)}"));

    // Creates a string that can be appended to an issue description to explain that the issue
    // is only for some versions of UAP.
    string CreateUapVersionRangeQualifier(UapVersionRange uapVersionRange)
    {
        // Ensure ranges are expressed consistently.
        uapVersionRange.NormalizeForMinimumVersion(_minimumUapVersion);

        if (uapVersionRange.Start.HasValue)
        {
            if (uapVersionRange.End.HasValue)
            {
                if (uapVersionRange.End == uapVersionRange.Start)
                {
                    return $" Affects only UAP version {uapVersionRange.Start}.";
                }
                else
                {
                    return $" Affects UAP versions from {uapVersionRange.Start} up to and including {uapVersionRange.End}.";
                }
            }
            else
            {
                return $" Affects UAP versions from {uapVersionRange.Start} and later.";
            }
        }
        else if (uapVersionRange.End.HasValue)
        {
            return $" Affects UAP versions up to and including {uapVersionRange.End}.";
        }
        else
        {
            return string.Empty;
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

    bool TryEnsureTranslated(LottieComposition lottieComposition)
    {
        if (_isTranslatedSuccessfully.HasValue)
        {
            return _isTranslatedSuccessfully.Value;
        }

        var configuration = new TranslatorConfiguration
        {
            AddCodegenDescriptions = true,
            TranslatePropertyBindings = true,
            GenerateColorBindings = _options.GenerateColorBindings,
            TargetUapVersion = _options.TargetUapVersion ?? uint.MaxValue,
        };

        var translationResult = LottieToMultiVersionWinCompTranslator.TryTranslateLottieComposition(
            lottieComposition: lottieComposition,
            configuration: configuration,
            minimumUapVersion: _options.WinUIVersion.Major >= 3 ? uint.MaxValue : _minimumUapVersion);

        _translationResults = translationResult.TranslationResults;
        _translationIssues = translationResult.Issues;

        _profiler.OnTranslateFinished();

        // Translation succeeded if all version produced root Visuals
        _isTranslatedSuccessfully = !_translationResults.Any(tr => tr.RootVisual is null);

        foreach (var (issue, uapVersionRange) in _translationIssues)
        {
            _reporter.WriteInfo(InfoType.Issue, IssueToString(_jsonFilePath, issue, uapVersionRange));
        }

        // NOTE: this is only reporting on the latest version in a multi-version translation.
        _beforeOptimizationStats = new Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.Stats(_translationResults[0].RootVisual);
        _profiler.OnUnmeasuredFinished();

        if (_isTranslatedSuccessfully.Value)
        {
            // Optimize the code unless told not to.
            if (!_options.DisableTranslationOptimizer)
            {
                _translationResults = _translationResults.Select(tr => tr.WithDifferentRoot(Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.Optimizer.Optimize(tr.RootVisual!, ignoreCommentProperties: true))).ToArray();
                _profiler.OnOptimizationFinished();

                // NOTE: this is only reporting on the latest version in a multi-version translation.
                _afterOptimizationStats = new Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.Stats(_translationResults[0].RootVisual);
                _profiler.OnUnmeasuredFinished();
            }
        }

        return _isTranslatedSuccessfully.Value;
    }

    void WriteAssetFiles(IEnumerable<Uri> assetList)
    {
        foreach (var a in assetList)
        {
            _reporter.WriteInfo(InfoType.Advice, $"Generated code references {a}. Make sure your app can access this file.");
        }
    }

    // Convert namespaces to a normalized form: replace "::" with ".".
    static string NormalizeNamespace(string @namespace) => @namespace.Replace("::", ".");

    public override string ToString() => $"{nameof(LottieJsonFileProcessor)} {_jsonFilePath}";
}
