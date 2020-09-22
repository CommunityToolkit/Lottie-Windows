// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal enum Lang
{
    // Language wasn't recognized.
    Unknown,

    // Language specified was ambigious.
    Ambiguous,

    CSharp,
    Cx,
    Cppwinrt,
    LottieYaml,
    WinCompDgml,
    Stats,
}

sealed class CommandLineOptions
{
    readonly List<string> _languageStrings = new List<string>();

    internal bool DisableCodeGenOptimizer { get; private set; }

    internal bool DisableTranslationOptimizer { get; private set; }

    // The parse error, or null if the parse succeeded.
    // The error should be a sentence (starts with a capital letter, and ends with a period).
    internal string ErrorDescription { get; private set; }

    internal bool GenerateColorBindings { get; private set; }

    internal bool GenerateDependencyObject { get; private set; }

    internal bool HelpRequested { get; private set; }

    internal string InputFile { get; private set; }

    internal string Interface { get; private set; }

    internal IEnumerable<Lang> Languages { get; private set; }

    internal uint? MinimumUapVersion { get; private set; }

    internal string Namespace { get; private set; }

    internal string OutputFolder { get; private set; }

    internal bool Public { get; private set; }

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

    // Experimental feature to control code generation for WinUI3. Eventually there
    // will be separate versions of LottieGen for WinUI3 and system APIs so there will
    // be no need for this switch. For now, if you're using LottieGen to generate
    // code for WinUI3, set this switch and the codegen will need less hand fixing.
    internal bool WinUI3Mode { get; private set; }

    // Returns a command line equivalent to the current set of options. This is intended
    // for adding to generated code so that users can regenerate the code and know that
    // they got the set of options the same as a previous run. It does not include the
    // InputFile, OutputFolder, or Language options.
    internal string ToConfigurationCommandLine(string languageSwitch)
    {
        var sb = new StringBuilder();
        sb.Append(ThisAssembly.AssemblyName);

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

        if (!string.IsNullOrWhiteSpace(Interface))
        {
            sb.Append($" -{nameof(Interface)} {Interface}");
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

        // The -Public switch is ignored for cppwinrt.
        if (Public && !languageSwitch.Equals("cppwinrt", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append($" -{nameof(Public)}");
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

        if (WinUI3Mode)
        {
            sb.Append($" -{nameof(WinUI3Mode)}");
        }

        return sb.ToString();
    }

    enum Keyword
    {
        None = 0,
        Ambiguous,
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
        Strict,
        TargetUapVersion,
        TestMode,
        WinUI3Mode,
    }

    // Returns the parsed command line. If ErrorDescription is non-null, then the parse failed.
    internal static CommandLineOptions ParseCommandLine(string[] args)
    {
        var result = new CommandLineOptions();
        result.ParseCommandLineStrings(args);

        // Convert the language strings to language values.
        var languageTokenizer = new CommandlineTokenizer<Lang>(Lang.Ambiguous)
                .AddKeyword(Lang.CSharp)
                .AddKeyword(Lang.Cx, "cppcx")
                .AddKeyword(Lang.Cx)
                .AddKeyword(Lang.Cppwinrt)
                .AddKeyword(Lang.Cppwinrt, "winrtcpp")
                .AddKeyword(Lang.LottieYaml)
                .AddKeyword(Lang.WinCompDgml, "dgml")
                .AddKeyword(Lang.Stats);

        var languages = new List<Lang>();

        // Parse the language string.
        foreach (var languageString in result._languageStrings)
        {
            languageTokenizer.TryMatchKeyword(languageString, out var language);
            languages.Add(language);
            switch (language)
            {
                case Lang.Unknown:
                    result.ErrorDescription = $"Unrecognized language: {languageString}";
                    break;
                case Lang.Ambiguous:
                    result.ErrorDescription = $"Ambiguous language: {languageString}";
                    break;
            }
        }

        result.Languages = languages.Distinct();

        return result;
    }

    void ParseCommandLineStrings(string[] args)
    {
        // Define the keywords accepted on the command line.
        var tokenizer = new CommandlineTokenizer<Keyword>(Keyword.Ambiguous)
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
            .AddPrefixedKeyword(Keyword.Strict)
            .AddPrefixedKeyword(Keyword.TargetUapVersion)
            .AddPrefixedKeyword(Keyword.TestMode)
            .AddPrefixedKeyword(Keyword.WinUI3Mode);

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
                        case Keyword.WinUI3Mode:
                            WinUI3Mode = true;
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

                        // The following keywords require a parameter as the next token.
                        case Keyword.InputFile:
                        case Keyword.Interface:
                        case Keyword.Language:
                        case Keyword.Namespace:
                        case Keyword.OutputFolder:
                        case Keyword.MinimumUapVersion:
                        case Keyword.TargetUapVersion:
                            previousKeyword = keyword;
                            break;
                        default:
                            // Should never get here.
                            throw new InvalidOperationException();
                    }

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
                case Keyword.Interface:
                    if (Interface != null)
                    {
                        ErrorDescription = ArgumentSpecifiedMoreThanOnce("Interface");
                        return;
                    }

                    Interface = arg;
                    previousKeyword = Keyword.None;
                    break;
                case Keyword.Language:
                    _languageStrings.Add(arg);
                    break;
                case Keyword.Namespace:
                    if (Namespace != null)
                    {
                        ErrorDescription = ArgumentSpecifiedMoreThanOnce("Namespace");
                        return;
                    }

                    Namespace = arg;
                    break;
                case Keyword.OutputFolder:
                    if (OutputFolder != null)
                    {
                        ErrorDescription = ArgumentSpecifiedMoreThanOnce("Output folder");
                        return;
                    }

                    OutputFolder = arg;
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
}