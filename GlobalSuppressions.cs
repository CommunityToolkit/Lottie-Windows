// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "IAsyncAction is awaitable. Rule doesn't apply", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.LottieVisualSource.SetSourceAsync(System.Uri)~Windows.Foundation.IAsyncAction")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "IAsyncAction is awaitable. Rule doesn't apply", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.LottieVisualSource.SetSourceAsync(Windows.Storage.StorageFile)~Windows.Foundation.IAsyncAction")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment must be preceded by blank line", Justification = "Not valid in a parameter list", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.LottieToWinCompTranslator.ApplyPathKeyFrameAnimation(Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.LottieToWinCompTranslator.TranslationContext,Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Animatable{Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Sequence{Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.BezierSegment}},Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.ShapeFill.PathFillType,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.CompositionObject,System.String,System.String,System.String)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment must be preceded by blank line", Justification = "Space in the middle of a parameter list seems wrong", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.LottieToWinCompTranslator.ApplyPathKeyFrameAnimation(Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp.LottieToWinCompTranslator.TranslationContext,Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Animatable{Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Sequence{Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.BezierSegment}},Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.ShapeFill.PathFillType,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.CompositionObject,System.String,System.String,System.String)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment must be preceded by blank line", Justification = "This is a space in the middle of an expression", Scope = "member", Target = "~M:Program.TryGenerateCode(System.String,System.String,System.String,System.Boolean,Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats@,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats@,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats@)~System.Boolean")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1515:Single-line comment should be preceded by blank line", Justification = "Space in the middle of a parameter list seems wrong", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.CxInstantiatorGenerator.CreateFactoryCode(System.String,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Visual,System.Single,System.Single,System.TimeSpan,System.String,System.String@,System.String@,System.String@,System.Boolean)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114:Parameter list must follow declaration", Justification = "Commented out code", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.LottieVisualSource.TryCreateAnimatedVisual(Windows.UI.Composition.Compositor,System.Object@)~Microsoft.UI.Xaml.Controls.IAnimatedVisual")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1124:Do not use regions", Justification = "Disagree in this case", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.LottieVisualSource.CheckedAwaitAsync(System.Threading.Tasks.Task)~System.Threading.Tasks.Task")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:Commas must be spaced correctly", Justification = "No space because it's a string format specifier", Scope = "member", Target = "~M:Program.WriteCodeGenStatsReport(System.IO.TextWriter,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats,Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools.Stats)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1001:Commas must be spaced correctly", Justification = "No space because it's a string format specifier", Scope = "member", Target = "~M:Program.WriteLottieStatsReport(System.IO.TextWriter,Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Tools.Stats)")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:Variable names should begin with lower-case letter", Justification = "Rule is incorrect on anonymous tuple types.", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.TreeReducer.RemoveEmptyContainers(Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.ObjectGraph{Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.TreeReducer.Node})")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:Variable names should begin with lower-case letter", Justification = "Rule is incorrect on anonymous tuple types.", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.TreeReducer.CoalesceContainerShapes(Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.ObjectGraph{Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.TreeReducer.Node})")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:Variable names should begin with lower-case letter", Justification = "Rule is incorrect on anonymous tuple types.", Scope = "member", Target = "~M:Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.TreeReducer.CoalesceContainerVisuals(Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.ObjectGraph{Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools.TreeReducer.Node})")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Fix is too complex for now", Scope = "type", Target = "~T:Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.CheckedJsonObject")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Fix is too complex for now", Scope = "type", Target = "~T:Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.CheckedJsonArray")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Fix is too complex for now", Scope = "type", Target = "~T:Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.JObjectExtensions")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Fix is too complex for now", Scope = "type", Target = "~T:Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization.JTokenExtensions")]