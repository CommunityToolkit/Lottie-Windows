// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieGen;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieGenExe
{
    sealed class Main
    {
        readonly CommandLineOptions _options;
        readonly Reporter _reporter;

        enum RunResult
        {
            Success,
            InvalidUsage,
            Failure,
        }

        internal static int Run(string[] args)
        {
            var reporter = new Reporter(infoStream: Console.Out, errorStream: Console.Out);

            switch (new Main(CommandLineOptions.ParseCommandLine(args), reporter).Run())
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

        Main(CommandLineOptions options, Reporter reporter)
        {
            _options = options;
            _reporter = reporter;
        }

        RunResult Run()
        {
            // Sign on
            var assemblyVersion = ThisAssembly.AssemblyInformationalVersion;

            var toolNameAndVersion = $"Lottie for Windows Code Generator version {assemblyVersion}";
            _reporter.WriteInfo(InfoType.Signon, toolNameAndVersion);
            _reporter.WriteInfoNewLine();

            if (_options.ErrorDescription != null)
            {
                // Failed to parse the command line.
                _reporter.WriteError("Invalid arguments.");
                _reporter.ErrorStream.WriteLine(_options.ErrorDescription);
                return RunResult.InvalidUsage;
            }
            else if (_options.WinUIVersion.Major >= 3 && (_options.MinimumUapVersion.HasValue || _options.TargetUapVersion.HasValue))
            {
                // WinUI3 does not permit setting a minimum or target version because WinUI3 implies
                // the latest version only.
                _reporter.WriteError($"WinUI versions of 3 and above cannot be used with {nameof(_options.MinimumUapVersion)} or {nameof(_options.TargetUapVersion)}.");
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

            // Check for required args.
            if (_options.InputFile is null)
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
                if (language == Language.Unknown)
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
#else
                Parallel.ForEach(matchingInputFiles, ((string path, string relativePath) inputFile) =>
#endif // DO_NOT_PROCESS_IN_PARALLEL
                {
                    if (!FileProcessor.ProcessFile(
                        _options,
                        _reporter,
                        inputFile.path,
                        Path.Combine(outputFolder, inputFile.relativePath),
                        timestamp))
                    {
                        succeeded = false;
                    }
                });
            }
            catch (Exception e)
            {
                _reporter.WriteError(e.ToString());
                return RunResult.Failure;
            }

            if (_options.Languages.Contains(Language.Stats))
            {
                // Write the stats. Stats are collected by the Reporter from each FileProcessor
                // then written to files here after all of the FileProcessors have finished.
                foreach (var (dataTableName, columnNames, rows) in _reporter.GetDataTables())
                {
                    var tsvFilePath = Path.Combine(outputFolder, $"LottieGen_{dataTableName}.tsv");
                    _reporter.WriteInfo("Writing stats to:");
                    _reporter.WriteInfo(InfoType.FilePath, $" {tsvFilePath}");
                    using var tsvFile = File.CreateText(tsvFilePath);
                    tsvFile.WriteLine(string.Join("\t", columnNames));

                    // Sort the rows. This is necessary in order to ensure deterministic output
                    // when multiple FileProcessors are run in parallel.
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

            return succeeded ? RunResult.Success : RunResult.Failure;
        }

        static string MakeAbsolutePath(string path) =>
            Path.IsPathRooted(path)
                ? path
                : Path.Combine(Directory.GetCurrentDirectory(), path);

        static void ShowHelp(Reporter.Writer writer)
        {
            writer.WriteLine("Generates source code from Lottie .json files.");
            writer.WriteLine();
            ShowUsage(writer);
        }

        static void ShowUsage(Reporter.Writer writer)
            => writer.WriteLine(Usage.Text);
    }
}