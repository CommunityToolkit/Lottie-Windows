// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieGenExe
{
    sealed class Usage
    {
        internal static string Text
        {
            get
            {
                // Note that we can't get the assembly name from Assembly.GetEntryAssembly
                // because we may be running as a .NET single file app.
                var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

                return
    @$"
Usage: {exeName} -InputFile LOTTIEFILE -Language LANG [Other options]

OVERVIEW:
       Generates source code from Lottie files for playing in the AnimatedVisualPlayer.
       LOTTIEFILE is a Lottie .json file or .lottie file. LOTTIEFILE may contain wildcards.
       LANG is one of cs, cppcx, cppwinrt, lottieyaml, dgml, or stats.
       -Language LANG may be specified multiple times.

       [Other options]

         -Help         Print this help message and exit.
         -AdditionalInterface
                       Specifies an additional interface that the generated code
                       will claim to implement. May be specified multiple times.
         -DisableTranslationOptimizer
                       Disables optimization of the translation from Lottie to
                       Windows code. Mainly used to detect bugs in the optimizer.
         -DisableCodeGenOptimizer
                       Disables optimization done by the code generator. This is
                       useful when the generated code is going to be hacked on.
         -GenerateColorBindings
                       Generates properties for each distinct color of fills and
                       strokes so that the colors in the animation can be modified
                       at runtime.
         -GenerateDependencyObject
                       Generates code that extends DependencyObject. This is useful
                       to allow XAML binding to properties in the Lottie source.
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
         -Public       Makes the generated class public rather than internal. Ignored
                       for c++.
         -RootNamespace
                       Cppwinrt only, specifies the root namespace of the consuming
                       project. Affects the names used to reference files generated
                       by cppwinrt.exe.
         -StrictMode   Fails on any parsing or translation issue. If not specified,
                       a best effort will be made to create valid output, and any
                       issues will be reported to STDOUT.
         -TargetUapVersion
                       The target UAP version on which the result will run. Must be 7
                       or higher and >= MinimumUapVersion. This value determines the
                       minimum SDK version required to compile the generated code.
                       If not specified, defaults to the latest UAP version.
         -TestMode     Prevents any information from being included that could change
                       from run to run with the same inputs, for example tool version
                       numbers, file paths, and dates. This is designed to enable
                       testing of the tool by diffing the outputs.
         -WinUIVersion Generates code for a particular WinUI version. Defaults to 2.4.

EXAMPLES:

       Generate Foo.cpp and Foo.h cppwinrt files in the current directory from the 
       Lottie file Foo.json:

         {exeName} -InputFile Foo.json -Language cppwinrt


       Keywords can be abbreviated and are case insensitive.
       Generate Bar.cs in the C:\temp directory from the Lottie file Bar.json:

         {exeName} -inp Bar.json -L cs -o C:\temp";
            }
        }
    }
}