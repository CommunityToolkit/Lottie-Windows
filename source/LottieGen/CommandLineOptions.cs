// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieGen
{
    sealed class CommandLineOptions
    {
        readonly List<string> _additionalInterfaces = new List<string>();
        readonly List<string> _languageStrings = new List<string>();
        Version? _winUIVersion;

        internal IReadOnlyList<string> AdditionalInterfaces => _additionalInterfaces;

        internal bool DisableCodeGenOptimizer { get; private set; }

        internal bool DisableTranslationOptimizer { get; private set; }

        // The parse error, or null if the parse succeeded.
        // The error should be a sentence (starts with a capital letter, and ends with a period).
        internal string? ErrorDescription { get; private set; }

        internal bool GenerateColorBindings { get; private set; }

        internal bool GenerateDependencyObject { get; private set; }

        internal bool HelpRequested { get; private set; }

        internal string? InputFile { get; private set; }

        internal IReadOnlyList<Language> Languages { get; private set; } = Array.Empty<Language>();

        internal uint? MinimumUapVersion { get; private set; }

        internal string? Namespace { get; private set; }

        internal string? OutputFolder { get; private set; }

        internal bool Public { get; private set; }

        internal string? RootNamespace { get; private set; }

        internal bool StrictMode { get; private set; }

        internal uint? TargetUapVersion { get; private set; }

        // TestMode causes the output to not contain any information that would
        // change from run to run given the same inputs. For example, the output
        // will not contain any tool version number, any dates, or any path
        // information.
        //
        // This mode is designed to allow testing by comparing the output of
        // a previous version of the tool.
        internal bool TestMode { get; private set; }

        // If not specified, default to version 2.4 as that is the version that
        // was current when we added the WinUIVersion parameter. That way the
        // old users won't be broken by this change.
        internal Version WinUIVersion => _winUIVersion ?? new Version(2, 4);

        // Undocumented - use the intermediate representation and translatore.
#pragma warning disable SA1300 // Element should begin with upper-case letter
        internal bool _UseIR { get; private set; }
#pragma warning restore SA1300 // Element should begin with upper-case letter

        // Returns a command line equivalent to the current set of options. This is intended
        // for adding to generated code so that users can regenerate the code and know that
        // they got the set of options the same as a previous run. It does not include the
        // InputFile, OutputFolder, or Language options.
        internal string ToConfigurationCommandLine(Language languageSwitch)
        {
            var sb = new StringBuilder();
            sb.Append(ThisAssembly.AssemblyName);

            if (AdditionalInterfaces.Any())
            {
                foreach (var additionalInterface in AdditionalInterfaces)
                {
                    sb.Append($" -{nameof(Keyword.AdditionalInterface)} {additionalInterface}");
                }
            }

            if (DisableCodeGenOptimizer)
            {
                sb.Append($" -{nameof(DisableCodeGenOptimizer)}");
            }

            if (DisableTranslationOptimizer)
            {
                sb.Append($" -{nameof(DisableTranslationOptimizer)}");
            }

            if (GenerateColorBindings)
            {
                sb.Append($" -{nameof(GenerateColorBindings)}");
            }

            if (GenerateDependencyObject)
            {
                sb.Append($" -{nameof(GenerateDependencyObject)}");
            }

            sb.Append($" -Language {languageSwitch}");

            if (MinimumUapVersion.HasValue)
            {
                sb.Append($" -{nameof(MinimumUapVersion)} {MinimumUapVersion.Value}");
            }

            if (!string.IsNullOrWhiteSpace(Namespace))
            {
                sb.Append($" -{nameof(Namespace)} {Namespace}");
            }

            switch (languageSwitch)
            {
                case Language.Cx:
                case Language.Cppwinrt:
                    // The -Public switch is ignored for c++.
                    break;

                default:
                    sb.Append($" -{nameof(Public)}");
                    break;
            }

            switch (languageSwitch)
            {
                case Language.Cppwinrt:
                    // The -RootNamespace parameter is only used for cppwinrt.
                    if (!string.IsNullOrWhiteSpace(RootNamespace))
                    {
                        sb.Append($" -{nameof(RootNamespace)} {RootNamespace}");
                    }

                    break;
            }

            if (StrictMode)
            {
                sb.Append($" -{nameof(StrictMode)}");
            }

            if (TargetUapVersion.HasValue)
            {
                // Only include the target if it is greater than the minimum, because
                // if it is the same as the minimum it is redundant.
                if (!MinimumUapVersion.HasValue || MinimumUapVersion < TargetUapVersion)
                {
                    sb.Append($" -{nameof(TargetUapVersion)} {TargetUapVersion.Value}");
                }
            }

            sb.Append($" -{nameof(WinUIVersion)} {WinUIVersion.ToString(2)}");

            return sb.ToString();
        }

        enum Keyword
        {
            // Special value ot indicate that there was no match, or unintialized.
            None = 0,

            // Special value to indicate that more than one keyword matched.
            Ambiguous,

            // Normal keywords.
            AdditionalInterface,
            DisableCodeGenOptimizer,
            DisableTranslationOptimizer,
            GenerateColorBindings,
            GenerateDependencyObject,
            Help,
            InputFile,
            Interface,
            Language,
            MinimumUapVersion,
            Namespace,
            OutputFolder,
            Public,
            RootNamespace,
            Strict,
            TargetUapVersion,
            TestMode,
            WinUIVersion,

            // Undocumented keywords. Always start with an underscore.
#pragma warning disable SA1300 // Element should begin with upper-case letter
            _UseIR,
#pragma warning restore SA1300 // Element should begin with upper-case letter
        }

        // Returns the parsed command line. If ErrorDescription is non-null, then the parse failed.
        internal static CommandLineOptions ParseCommandLine(string[] args)
        {
            var result = new CommandLineOptions();
            result.ParseCommandLineStrings(args);

            // Convert the language strings to language values.
            var languageTokenizer = new CommandlineTokenizer<Language>(Language.Ambiguous)
                    .AddKeyword(Language.CSharp)
                    .AddKeyword(Language.Cx, "cppcx")
                    .AddKeyword(Language.Cx)
                    .AddKeyword(Language.Cppwinrt)
                    .AddKeyword(Language.Cppwinrt, "winrtcpp")
                    .AddKeyword(Language.LottieYaml)
                    .AddKeyword(Language.WinCompDgml, "dgml")
                    .AddKeyword(Language.Stats);

            var languages = new List<Language>();

            // Parse the language string.
            foreach (var languageString in result._languageStrings)
            {
                languageTokenizer.TryMatchKeyword(languageString, out var language);
                languages.Add(language);
                switch (language)
                {
                    case Language.Unknown:
                        result.ErrorDescription = $"Unrecognized language: {languageString}";
                        break;
                    case Language.Ambiguous:
                        result.ErrorDescription = $"Ambiguous language: {languageString}";
                        break;
                }
            }

            result.Languages = languages.Distinct().ToArray();

            // Sort any additional interfaces and remove duplicates.
            var additionalInterfaces = result._additionalInterfaces.OrderBy(name => name).Distinct().ToArray();
            result._additionalInterfaces.Clear();
            result._additionalInterfaces.AddRange(additionalInterfaces);

            return result;
        }

        void ParseCommandLineStrings(string[] args)
        {
            // Define the keywords accepted on the command line.
            var tokenizer = new CommandlineTokenizer<Keyword>(ambiguousValue: Keyword.Ambiguous)
                .AddPrefixedKeyword(Keyword.AdditionalInterface)
                .AddPrefixedKeyword(Keyword.DisableCodeGenOptimizer)
                .AddPrefixedKeyword(Keyword.DisableTranslationOptimizer)
                .AddPrefixedKeyword(Keyword.GenerateColorBindings)
                .AddPrefixedKeyword(Keyword.GenerateDependencyObject)
                .AddPrefixedKeyword(Keyword.Help, "?")
                .AddPrefixedKeyword(Keyword.Help)
                .AddPrefixedKeyword(Keyword.InputFile)
                .AddPrefixedKeyword(Keyword.Interface)
                .AddPrefixedKeyword(Keyword.Language)
                .AddPrefixedKeyword(Keyword.MinimumUapVersion)
                .AddPrefixedKeyword(Keyword.Namespace)
                .AddPrefixedKeyword(Keyword.OutputFolder)
                .AddPrefixedKeyword(Keyword.Public)
                .AddPrefixedKeyword(Keyword.RootNamespace)
                .AddPrefixedKeyword(Keyword.Strict)
                .AddPrefixedKeyword(Keyword.TargetUapVersion)
                .AddPrefixedKeyword(Keyword.TestMode)
                .AddPrefixedKeyword(Keyword.WinUIVersion)
                .AddPrefixedKeyword(Keyword._UseIR);

            // The last keyword recognized. This defines what the following parameter value is for,
            // or None if not expecting a parameter value.
            var previousKeyword = Keyword.None;

            foreach (var (keyword, arg) in tokenizer.Tokenize(args))
            {
                var prev = previousKeyword;
                previousKeyword = Keyword.None;
                switch (prev)
                {
                    case Keyword.None:
                        // Expecting a keyword.
                        switch (keyword)
                        {
                            case Keyword.Ambiguous:
                                ErrorDescription = $"Ambiguous: \"{arg}\".";
                                return;
                            case Keyword.None:
                                ErrorDescription = $"Unexpected: \"{arg}\".";
                                return;
                            case Keyword.GenerateColorBindings:
                                GenerateColorBindings = true;
                                break;
                            case Keyword.GenerateDependencyObject:
                                GenerateDependencyObject = true;
                                break;
                            case Keyword.Help:
                                HelpRequested = true;
                                return;
                            case Keyword.Strict:
                                StrictMode = true;
                                break;
                            case Keyword.TestMode:
                                TestMode = true;
                                break;
                            case Keyword.DisableCodeGenOptimizer:
                                DisableCodeGenOptimizer = true;
                                break;
                            case Keyword.DisableTranslationOptimizer:
                                DisableTranslationOptimizer = true;
                                break;
                            case Keyword.Public:
                                Public = true;
                                break;
                            case Keyword._UseIR:
                                _UseIR = true;
                                break;

                            // The following keywords require a parameter as the next token.
                            case Keyword.AdditionalInterface:
                            case Keyword.InputFile:
                            case Keyword.Interface:
                            case Keyword.Language:
                            case Keyword.Namespace:
                            case Keyword.OutputFolder:
                            case Keyword.MinimumUapVersion:
                            case Keyword.RootNamespace:
                            case Keyword.TargetUapVersion:
                            case Keyword.WinUIVersion:
                                previousKeyword = keyword;
                                break;
                            default:
                                // Should never get here.
                                throw new InvalidOperationException();
                        }

                        break;

                    case Keyword.AdditionalInterface:
                        _additionalInterfaces.Add(arg);
                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.InputFile:
                        if (InputFile != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("Input");
                            return;
                        }

                        InputFile = arg;
                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.Language:
                        _languageStrings.Add(arg);
                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.Namespace:
                        if (Namespace != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("Namespace");
                            return;
                        }

                        Namespace = arg;
                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.OutputFolder:
                        if (OutputFolder != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("Output folder");
                            return;
                        }

                        OutputFolder = arg;
                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.MinimumUapVersion:
                        if (MinimumUapVersion != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("Minimum UAP version");
                            return;
                        }

                        {
                            if (!uint.TryParse(arg, out var version))
                            {
                                ErrorDescription = ArgumentMustBeAPositiveInteger("Minimum UAP version");
                                return;
                            }

                            MinimumUapVersion = version;
                        }

                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.RootNamespace:
                        if (RootNamespace != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("Output folder");
                            return;
                        }

                        RootNamespace = arg;
                        previousKeyword = Keyword.None;
                        break;
                    case Keyword.TargetUapVersion:
                        if (TargetUapVersion != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("Target UAP version");
                            return;
                        }

                        {
                            if (!uint.TryParse(arg, out var version))
                            {
                                ErrorDescription = ArgumentMustBeAPositiveInteger("Target UAP version");
                                return;
                            }

                            TargetUapVersion = version;
                        }

                        previousKeyword = Keyword.None;
                        break;

                    case Keyword.WinUIVersion:
                        if (_winUIVersion != null)
                        {
                            ErrorDescription = ArgumentSpecifiedMoreThanOnce("WinUI version");
                            return;
                        }

                        {
                            if (!Version.TryParse(arg, out var version))
                            {
                                ErrorDescription = ArgumentMustBeAMajorAndMinorVerion("WinUI version");
                                return;
                            }

                            _winUIVersion = version;
                        }

                        previousKeyword = Keyword.None;
                        break;
                    default:
                        // Should never get here.
                        throw new InvalidOperationException();
                }
            }

            // All tokens consumed. Ensure we are not waiting for the final parameter value.
            if (previousKeyword != Keyword.None)
            {
                ErrorDescription = "Missing value.";
            }
        }

        static string ArgumentSpecifiedMoreThanOnce(string argument) => $"{argument} specified more than once.";

        static string ArgumentMustBeAPositiveInteger(string argument) => $"{argument} must be a positive integer.";

        static string ArgumentMustBeAMajorAndMinorVerion(string argument) => $"{argument} is not a version in the form M.m.";
    }
}