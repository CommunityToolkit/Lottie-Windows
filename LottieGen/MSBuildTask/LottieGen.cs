// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

namespace LottieGen.Task
{
    /// <summary>
    /// An MsBuild task for translating Lottie files to code.
    /// </summary>
    /// <remarks>
    /// Ideally this would run LottieGen in process, however
    /// MsBuild still runs on .NET Framework which is missing support
    /// for some .NET 5 features needed by the LottieGen code.
    /// So for now this is a wrapper around LottieGen.exe, which
    /// can be packaged with this Task as a self-contained
    /// executable.
    /// When MsBuild updates to .NET 5, this task can contain the
    /// LottieGen code and can call it in-process.
    /// </remarks>
    public sealed class LottieGen : Microsoft.Build.Utilities.ToolTask
    {
        protected override string ToolName => "LottieGen.exe";

        public string? LottieGenExePath { get; set; }

        /// <summary>
        /// The Lottie file to process. May contain wildcards.
        /// </summary>
        [Required]
        public string? InputFile { get; set; }

        /// <summary>
        /// cs, cppcx, or cppwinrt.
        /// </summary>
        [Required]
        public string? Language { get; set; }

        /// <summary>
        /// Specifies additional interfaces that the generated code
        /// will claim to implement.
        /// </summary>
        public ITaskItem[]? AdditionalInterface { get; set; }

        /// <summary>
        /// Disables optimization done by the code generator. This is
        /// useful when the generated code is going to be hacked on.
        /// </summary>
        public bool DisableCodeGenOptimizer { get; set; }

        /// <summary>
        /// Disables optimization of the translation from Lottie to
        /// Windows code.Mainly used to detect bugs in the optimizer.
        /// </summary>
        public bool DisableTranslationOptimizer { get; set; }

        /// <summary>
        /// Generates properties for each distinct color of fills and
        /// strokes so that the colors in the animation can be modified
        /// at runtime.
        /// </summary>
        public bool GenerateColorBindings { get; set; }

        /// <summary>
        /// Generates code that extends DependencyObject.This is useful
        /// to allow XAML binding to properties in the Lottie source.
        /// </summary>
        public bool GenerateDependencyObject { get; set; }

        /// <summary>
        /// The lowest UAP version on which the result must run.Defaults
        /// to 7. Must be 7 or higher.Code will be generated that will
        /// run down to this version.If less than TargetUapVersion,
        /// extra code will be generated if necessary to support the
        /// lower versions.
        /// </summary>
        public uint? MinimumUapVersion { get; set; }

        /// <summary>
        /// Specifies the namespace for the generated code. Defaults to
        /// AnimatedVisuals.
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// Specifies the output folder for the generated files.If not
        /// specified the files will be written to the current directory.
        /// </summary>
        [Required]
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Makes the generated class public rather than internal. Ignored
        /// for c++.
        /// </summary>
        public bool Public { get; set; }

        /// <summary>
        /// Cppwinrt only, specifies the root namespace of the consuming
        /// project. Affects the names used to reference files generated
        /// by cppwinrt.exe.
        /// </summary>
        public string? RootNamespace { get; set; }

        /// <summary>
        /// Fails on any parsing or translation issue. If not specified,
        /// a best effort will be made to create valid output, and any
        /// issues will be reported to STDOUT.
        /// </summary>
        public bool StrictMode { get; set; }

        /// <summary>
        /// The target UAP version on which the result will run.Must be 7
        /// or higher and >= MinimumUapVersion.This value determines the
        /// minimum SDK version required to compile the generated code.
        /// If not specified, defaults to the latest UAP version.
        /// </summary>
        public uint? TargetUapVersion { get; set; }

        /// <summary>
        /// Prevents any information from being included that could change
        /// from run to run with the same inputs, for example tool version
        /// numbers, file paths, and dates.This is designed to enable
        /// testing of the tool by diffing the outputs.
        /// </summary>
        public bool TestMode { get; set; }

        /// <summary>
        /// Generates code for a particular WinUI version. Defaults to 2.4.
        /// </summary>
        public Version? WinUIVersion { get; set; }

        protected override string GenerateCommandLineCommands()
        {
            var args = new List<string>();

            AddArg(nameof(InputFile), InputFile!);
            AddArg(nameof(Language), Language!);

            if (AdditionalInterface != null)
            {
                foreach (var value in AdditionalInterface)
                {
                    AddArg("additionalInterface", value.ItemSpec);
                }
            }

            AddOptionalBool(nameof(DisableCodeGenOptimizer), DisableCodeGenOptimizer);
            AddOptionalBool(nameof(DisableTranslationOptimizer), DisableTranslationOptimizer);
            AddOptionalBool(nameof(GenerateColorBindings), GenerateColorBindings);
            AddOptionalBool(nameof(GenerateDependencyObject), GenerateDependencyObject);
            AddOptional(nameof(MinimumUapVersion), MinimumUapVersion);
            AddOptional(nameof(Namespace), Namespace);
            AddArg(nameof(OutputFolder), OutputFolder!);
            AddOptionalBool(nameof(Public), Public);
            AddOptional(nameof(RootNamespace), RootNamespace);
            AddOptionalBool(nameof(StrictMode), StrictMode);
            AddOptional(nameof(TargetUapVersion), TargetUapVersion);
            AddOptional(nameof(WinUIVersion), WinUIVersion);

            var result = string.Join(" ", args);

            return result;

            void AddOptional(string parameterName, object? value)
            {
                if (value != null)
                {
                    AddArg(parameterName, value.ToString());
                }
            }

            void AddOptionalBool(string parameterName, bool value)
            {
                if (value)
                {
                    AddArg(parameterName, "true");
                }
            }

            void AddArg(string parameterName, string value)
                => args.Add($"-{parameterName} {value}");
        }

        // Provides the default path to the tool. Ignored if
        // the <ToolPath/> property is set.
        // By default we expect the tool to be in the same directory
        // as the assembly that this class is in.
        protected override string GenerateFullPathToTool()
            => Path.Combine(Assembly.GetExecutingAssembly().Location, ToolName);
    }
}
