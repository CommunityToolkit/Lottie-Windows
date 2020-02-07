// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp;

sealed class Program
{
    readonly CommandLineOptions _options;
    readonly Reporter _reporter;

    enum RunResult
    {
        Success,
        InvalidUsage,
        Failure,
    }

    static int Main(string[] args)
    {
        var reporter = new Reporter(infoStream: Console.Out, errorStream: Console.Out);

        switch (new Program(CommandLineOptions.ParseCommandLine(args), reporter).Run())
        {
            case RunResult.Success:
                return 0;

            case RunResult.Failure:
                return 1;

            case RunResult.InvalidUsage:
                reporter.ErrorStream.WriteLine();
                ShowUsage(reporter.ErrorStream);
                return 1;

            default:
                // Should never get here.
                throw new InvalidOperationException();
        }
    }

    Program(CommandLineOptions options, Reporter reporter)
    {
        _options = options;
        _reporter = reporter;
    }

    RunResult Run()
    {
        // Sign on
        var assemblyVersion = ThisAssembly.AssemblyInformationalVersion;

        var toolNameAndVersion = $"Lottie for Windows Code Generator version {assemblyVersion}";
        _reporter.WriteInfo(toolNameAndVersion);
        _reporter.WriteInfoNewLine();

        if (_options.ErrorDescription != null)
        {
            // Failed to parse the command line.
            _reporter.WriteError("Invalid arguments.");
            _reporter.ErrorStream.WriteLine(_options.ErrorDescription);
            return RunResult.InvalidUsage;
        }
        else if (_options.MinimumUapVersion.HasValue && _options.MinimumUapVersion < LottieToWinCompTranslator.MinimumTargetUapVersion)
        {
            // Unacceptable version.
            _reporter.WriteError($"Invalid minimum UAP version \"{_options.MinimumUapVersion}\". Must be 7 or above.");
            return RunResult.InvalidUsage;
        }
        else if (_options.TargetUapVersion.HasValue)
        {
            if (_options.TargetUapVersion < 7)
            {
                // Unacceptable version.
                _reporter.WriteError($"Invalid target UAP version \"{_options.TargetUapVersion}\". Must be 7 or above.");
                return RunResult.InvalidUsage;
            }

            if (_options.MinimumUapVersion.HasValue && _options.TargetUapVersion < _options.MinimumUapVersion)
            {
                // Unacceptable version.
                _reporter.WriteError($"Invalid target UAP version \"{_options.TargetUapVersion}\". Must be greater than the minimum UAP version specified ({_options.MinimumUapVersion}).");
                return RunResult.InvalidUsage;
            }
        }
        else if (_options.HelpRequested)
        {
            ShowHelp(_reporter.InfoStream);
            return RunResult.Success;
        }

        // Check for required args
        if (_options.InputFile == null)
        {
            _reporter.WriteError("Lottie file not specified.");
            return RunResult.InvalidUsage;
        }

        // Validate the languages.
        if (!_options.Languages.Any())
        {
            _reporter.WriteError("Language not specified.");
            return RunResult.InvalidUsage;
        }

        foreach (var language in _options.Languages)
        {
            if (language == Lang.Unknown)
            {
                _reporter.WriteError("Invalid language.");
                return RunResult.InvalidUsage;
            }
        }

        // Check that at least one file matches InputFile.
        var matchingInputFiles = Glob.EnumerateFiles(_options.InputFile).ToArray();
        if (matchingInputFiles.Length == 0)
        {
            _reporter.WriteError($"File not found: {_options.InputFile}");
            return RunResult.Failure;
        }

        // Get the output folder as an absolute path, defaulting to the current directory
        // if no output folder was specified.
        var outputFolder = MakeAbsolutePath(_options.OutputFolder ?? Directory.GetCurrentDirectory());

        // Get a timestamp to include in the output to help identify a particular
        // run on the tool.
        var timestamp = DateTime.UtcNow;

        // Assume success.
        var succeeded = true;

        try
        {
#if DO_NOT_PROCESS_IN_PARALLEL
            foreach (var (file, relativePath) in matchingInputFiles)
            {
                if (!LottieFileProcessor.ProcessFile(
                    _options,
                    _reporter,
                    file,
                    System.IO.Path.Combine(outputFolder, relativePath),
                    timestamp))
                {
                    succeeded = false;
                }
            }
#else
            Parallel.ForEach(matchingInputFiles, ((string path, string relativePath) inputFile) =>
            {
                if (!LottieFileProcessor.ProcessFile(
                    _options,
                    _reporter,
                    inputFile.path,
                    System.IO.Path.Combine(outputFolder, inputFile.relativePath),
                    timestamp))
                {
                    succeeded = false;
                }
            });
#endif
        }
        catch (Exception e)
        {
            _reporter.WriteError(e.ToString());
            return RunResult.Failure;
        }

        if (_options.Languages.Contains(Lang.Stats))
        {
            // Write the stats. Stats are collected by the Reporter from each LottieFileProcessor
            // then written to files here after all of the LottieFileProcessors have finished.
            foreach (var (dataTableName, columnNames, rows) in _reporter.GetDataTables())
            {
                var tsvFilePath = System.IO.Path.Combine(outputFolder, $"LottieGen_{dataTableName}.tsv");
                _reporter.WriteInfo($"Writing stats to {tsvFilePath}");
                using (var tsvFile = File.CreateText(tsvFilePath))
                {
                    tsvFile.WriteLine(string.Join("\t", columnNames));

                    // Sort the rows. This is necessary in order to ensure deterministic output
                    // when multiple LottieFileProcessors are run in parallel.
                    Array.Sort(rows, (a, b) =>
                    {
                        for (var i = 0; i < a.Length; i++)
                        {
                            var result = StringComparer.Ordinal.Compare(a[i], b[i]);
                            if (result != 0)
                            {
                                return result;
                            }
                        }

                        return 0;
                    });

                    foreach (var row in rows)
                    {
                        tsvFile.WriteLine(string.Join("\t", row));
                    }
                }
            }
        }

        return succeeded ? RunResult.Success : RunResult.Failure;
    }

    static string MakeAbsolutePath(string path)
    {
        return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    static void ShowHelp(Reporter.Writer writer)
    {
        writer.WriteLine("Generates source code from Lottie .json files.");
        writer.WriteLine();
        ShowUsage(writer);
    }

    static void ShowUsage(Reporter.Writer writer)
    {
        writer.WriteLine(Usage);
    }

    static string Usage => string.Format(
@"
Usage: {0} -InputFile LOTTIEFILE -Language LANG [Other options]

OVERVIEW:
       Generates source code from Lottie files for playing in the AnimatedVisualPlayer. 
       LOTTIEFILE is a Lottie .json file. LOTTIEFILE may contain wildcards.
       LANG is one of cs, cppcx, cppwinrt, wincompxml, lottiexml, lottieyaml, dgml, or stats.
       -Language LANG may be specified multiple times.

       [Other options]

         -Help         Print this help message and exit.
         -DisableTranslationOptimizer  
                       Disables optimization of the translation from Lottie to
                       Windows code. Mainly used to detect bugs in the optimizer.
         -DisableCodeGenOptimizer
                       Disables optimization done by the code generator. This is
                       useful when the generated code is going to be hacked on.
         -GenerateDependencyObject
                       Generates code that extends DependencyObject. This is useful
                       to allow XAML binding to properties in the Lottie source.
         -Interface    Specifies the name of the interface to implement in the generated
                       code. Defaults to Microsoft.UI.Xaml.Controls.IAnimatedVisual.
         -MinimumUapVersion
                       The lowest UAP version on which the result must run. Defaults
                       to 7. Must be 7 or higher. Code will be generated that will
                       run down to this version. If less than TargetUapVersion,
                       extra code will be generated if necessary to support the
                       lower versions.
         -Namespace    Specifies the namespace for the generated code. Defaults to
                       AnimatedVisuals.
         -OutputFolder Specifies the output folder for the generated files. If not
                       specified the files will be written to the current directory.
         -Public       Makes the generated class public.
         -StrictMode   Fails on any parsing or translation issue. If not specified,
                       a best effort will be made to create valid output, and any
                       issues will be reported to STDOUT.
         -TargetUapVersion
                       The target UAP version on which the result will run. Must be 7
                       or higher and >= MinimumUapVersion. Code will be generated
                       that may take advantage of features in this version in order
                       to produce a better result. If not specified, defaults to
                       the latest UAP version.

EXAMPLES:

       Generate Foo.cpp and Foo.h cppwinrt files in the current directory from the 
       Lottie file Foo.json:

         {0} -InputFile Foo.json -Language cppwinrt


       Keywords can be abbreviated and are case insensitive.
       Generate Bar.cs in the C:\temp directory from the Lottie file Bar.json:

         {0} -inp Bar.json -L cs -o C:\temp",
System.IO.Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().ManifestModule.Name));
}