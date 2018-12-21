// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
        var infoStream = Console.Out;
        var errorStream = Console.Out;

        switch (new Program(CommandLineOptions.ParseCommandLine(args), infoStream: infoStream, errorStream: errorStream).Run())
        {
            case RunResult.Success:
                return 0;

            case RunResult.Failure:
                return 1;

            case RunResult.InvalidUsage:
                errorStream.WriteLine();
                ShowUsage(errorStream);
                return 1;

            default:
                // Should never get here.
                throw new InvalidOperationException();
        }
    }

    Program(CommandLineOptions options, TextWriter infoStream, TextWriter errorStream)
    {
        _options = options;
        _reporter = new Reporter(infoStream, errorStream);
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

        // Assume success.
        var succeeded = true;

#if DO_NOT_PROCESS_IN_PARALLEL
        foreach (var (file, relativePath) in matchingInputFiles)
        {
            succeeded &= LottieFileProcessor.ProcessFile(_options, _reporter, file, Path.Combine(outputFolder, relativePath));
        }
#else
        Parallel.ForEach(matchingInputFiles, (inputFile) =>
        {
            succeeded &= LottieFileProcessor.ProcessFile(_options, _reporter, inputFile.path, Path.Combine(outputFolder, inputFile.relativePath));
        });
#endif
        return succeeded ? RunResult.Success : RunResult.Failure;
    }

    static string MakeAbsolutePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    static void ShowHelp(TextWriter writer)
    {
        writer.WriteLine("Generates source code from Lottie .json files.");
        writer.WriteLine();
        ShowUsage(writer);
    }

    static void ShowUsage(TextWriter writer)
    {
        writer.WriteLine(Usage);
    }

    static string Usage => string.Format(
@"
Usage: {0} -InputFile LOTTIEFILE -Language LANG [Other options]

OVERVIEW:
       Generates source code from Lottie files for playing in the AnimatedVisualPlayer. 
       LOTTIEFILE is a Lottie .json file. LOTTIEFILE may contain wildcards.
       LANG is one of cs, cppcx, winrtcpp, wincompxml, lottiexml, dgml, or stats.
       -Language LANG may be specified multiple times.

       [Other options]

         -Help         Print this help message and exit.
         -ClassName    Uses the given class name for the generated code. If not 
                       specified the name is synthesized from the name of the Lottie 
                       file. The class name will be sanitized as necessary to be valid
                       for the language and will also be used as the base name of 
                       the output file(s).
         -DisableTranslationOptimizer  
                       Disables optimization of the translation from Lottie to
                       Windows code. Mainly used to detect bugs in the optimizer.
         -DisableCodeGenOptimizer
                       Disables optimization done by the code generator. This is 
                       useful when the generated code is going to be hacked on.
         -OutputFolder Specifies the output folder for the generated files. If not
                       specified the files will be written to the current directory.
         -Strict       Fails on any parsing or translation issue. If not specified, 
                       a best effort will be made to create valid output, and any 
                       issues will be reported to STDOUT.
         -Verbose      Outputs extra info to STDOUT.

EXAMPLES:

       Generate Foo.cpp and Foo.h winrtcpp files in the current directory from the 
       Lottie file Foo.json:

         {0} -InputFile Foo.json -Language winrtcpp


       Keywords can be abbreviated and are case insensitive.
       Generate Grotz.cs in the C:\temp directory from the Lottie file Bar.json:

         {0} -i Bar.json -L cs -ClassName Grotz -o C:\temp",
Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().ManifestModule.Name));
}