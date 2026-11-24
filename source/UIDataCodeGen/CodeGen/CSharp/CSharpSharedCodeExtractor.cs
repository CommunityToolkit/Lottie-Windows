// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen.CSharp
{
    /// <summary>
    /// Moves the code that is identical in every AnimatedVisual class of a multi-version
    /// IAnimatedVisualSource into an abstract base class that the AnimatedVisual classes
    /// derive from, so that the code exists only once.
    /// </summary>
    /// <remarks>
    /// The behavior of every AnimatedVisual class is unchanged: a method is shared only if
    /// its code is identical in every class, the fields it uses are moved to the base class
    /// with the same declarations (so all of the code of a class still uses a single storage
    /// location for each field), and the methods it calls that differ between the classes
    /// are declared abstract in the base class and overridden by each class.
    /// This works on the text produced by the C# generator, which has a regular layout:
    /// a class declaration, then the members of the class indented by one level, each
    /// member being a field or expression bodied member on one line, or a method whose
    /// body is delimited by braces at the member indentation.
    /// </remarks>
    static class CSharpSharedCodeExtractor
    {
        const string MemberIndent = "    ";

        // The fields that are assigned by the constructor of the AnimatedVisual classes.
        // They are readonly in the AnimatedVisual classes, but cannot be readonly in the base
        // class because the constructors of the AnimatedVisual classes assign them.
        static readonly HashSet<string> s_constructorAssignedFields = new HashSet<string>
        {
            "_c",
            "_reusableExpressionAnimation",
            "_themeProperties",
        };

        static readonly Regex s_stringLiteral = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);
        static readonly Regex s_identifier = new Regex(@"(?<![\w.])[A-Za-z_]\w*", RegexOptions.Compiled);
        static readonly Regex s_call = new Regex(@"(?<![\w.])(?<!new )([A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);
        static readonly Regex s_field = new Regex(@"^(?<modifiers>(?:(?:const|readonly|static) )*)(?<type>[\w.<>\[\], ]+?) (?<name>\w+)(?<initializer> = .*)?;$", RegexOptions.Compiled);
        static readonly Regex s_method = new Regex(@"^(?<static>static )?(?<returnType>[\w.<>\[\]]+) (?<name>\w+)\(", RegexOptions.Compiled);

        /// <summary>
        /// Extracts the code shared by all of the given AnimatedVisual classes into a base class.
        /// </summary>
        /// <param name="baseClassName">The name to give the base class.</param>
        /// <param name="classes">The text lines of each AnimatedVisual class, indented relative to the class declaration.</param>
        /// <returns>The lines of the base class followed by the lines of each rewritten class, or null if nothing can be shared.</returns>
        internal static (IReadOnlyList<string> BaseClass, IReadOnlyList<IReadOnlyList<string>> Classes)? Extract(
            string baseClassName,
            IReadOnlyList<IReadOnlyList<string>> classes)
        {
            if (classes.Count < 2)
            {
                return null;
            }

            var parsedClasses = new List<ClassText>();
            foreach (var lines in classes)
            {
                var parsed = ClassText.Parse(lines);
                if (parsed is null)
                {
                    // Unexpected layout. Leave the code as it is.
                    return null;
                }

                parsedClasses.Add(parsed);
            }

            var (sharedMethods, abstractMethods, sharedFields) = FindSharedMembers(parsedClasses);
            if (sharedMethods.Count == 0)
            {
                return null;
            }

            var first = parsedClasses[0];

            // Write the base class.
            var baseClass = new List<string>
            {
                $"abstract class {baseClassName}",
                "{",
            };

            foreach (var field in first.Members.Where(m => m.FieldName is not null && sharedFields.Contains(m.FieldName)))
            {
                var declaration = field.Declaration;
                if (s_constructorAssignedFields.Contains(field.FieldName!))
                {
                    declaration = declaration.Replace("readonly ", string.Empty);
                }

                baseClass.Add($"{MemberIndent}internal {declaration}");
            }

            foreach (var method in first.Members.Where(m => m.Signature is not null && sharedMethods.Contains(m.Signature)))
            {
                baseClass.Add(string.Empty);
                baseClass.AddRange(WithModifier(method, "internal "));
            }

            foreach (var method in first.Members.Where(m => m.Signature is not null && abstractMethods.Contains(m.Signature)))
            {
                baseClass.Add(string.Empty);
                baseClass.Add($"{MemberIndent}internal abstract {method.HeaderLines[0].Trim()};");
            }

            baseClass.Add("}");

            // Rewrite each class.
            var rewrittenClasses = new List<IReadOnlyList<string>>();
            foreach (var c in parsedClasses)
            {
                var result = new List<string>();
                result.Add(c.DeclarationLines[0]);
                result.Add($"{MemberIndent}: {baseClassName}");
                foreach (var line in c.DeclarationLines.Skip(1))
                {
                    // The interfaces follow the base class.
                    result.Add(line.StartsWith($"{MemberIndent}: ", StringComparison.Ordinal)
                        ? $"{MemberIndent}, {line.Substring(MemberIndent.Length + 2)}"
                        : line);
                }

                result.Add("{");
                foreach (var member in c.Members)
                {
                    if ((member.Signature is not null && sharedMethods.Contains(member.Signature)) ||
                        (member.FieldName is not null && sharedFields.Contains(member.FieldName)))
                    {
                        // Moved to the base class.
                        continue;
                    }

                    result.AddRange(member.LeadingLines);
                    if (member.Signature is not null && abstractMethods.Contains(member.Signature))
                    {
                        result.AddRange(WithModifier(member, "internal override "));
                    }
                    else
                    {
                        result.AddRange(member.CodeLines);
                    }
                }

                result.AddRange(c.TrailingLines);
                result.Add("}");
                rewrittenClasses.Add(result);
            }

            return (baseClass, rewrittenClasses);
        }

        // Returns the lines of the member with the given modifier added to its first code line.
        static IEnumerable<string> WithModifier(Member member, string modifier)
        {
            foreach (var line in member.CommentLines)
            {
                yield return line;
            }

            var firstCodeLine = true;
            foreach (var line in member.CodeLines.Where(l => !IsComment(l) || !firstCodeLine))
            {
                yield return firstCodeLine ? $"{MemberIndent}{modifier}{line.Substring(MemberIndent.Length)}" : line;
                firstCodeLine = false;
            }
        }

        static (HashSet<string> SharedMethods, HashSet<string> AbstractMethods, HashSet<string> SharedFields) FindSharedMembers(
            IReadOnlyList<ClassText> classes)
        {
            // Methods are identified by their signature because helper methods can be overloaded.
            var methodsByClass = classes.Select(c => c.Members.Where(m => m.Signature is not null).ToDictionary(m => m.Signature!)).ToArray();
            var fieldsByClass = classes.Select(c => c.Members.Where(m => m.FieldName is not null).ToDictionary(m => m.FieldName!)).ToArray();
            var signaturesByName = methodsByClass
                .SelectMany(d => d.Values)
                .GroupBy(m => m.MethodName!)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Signature!).Distinct().ToArray());
            var allFieldNames = new HashSet<string>(fieldsByClass.SelectMany(d => d.Keys));

            // Start with the methods that have identical code in every class.
            var shared = new HashSet<string>(
                methodsByClass[0].Values
                    .Where(m => m.IsShareable &&
                                methodsByClass.All(d => d.TryGetValue(m.Signature!, out var other) && other.Key == m.Key))
                    .Select(m => m.Signature!));

            // A field can be shared if it has the same declaration in every class.
            bool IsShareableField(string name)
                => fieldsByClass.All(d => d.TryGetValue(name, out var f) &&
                                          f.Declaration == fieldsByClass[0][name].Declaration &&
                                          (!f.IsReadonly || s_constructorAssignedFields.Contains(name)));

            // A method that is called from shared code but differs between classes must be overridable:
            // it must be an instance method with the same single line signature in every class.
            bool IsOverridable(string signature)
                => methodsByClass.All(d => d.TryGetValue(signature, out var m) &&
                                           m.IsShareable &&
                                           !m.IsStatic &&
                                           m.HeaderLines.Count == 1);

            // The signatures of the methods that a method may call. Calls are found by name, so
            // a call to an overloaded method may call any of the overloads.
            IEnumerable<string> Callees(Member method)
                => method.Calls.Where(signaturesByName.ContainsKey).SelectMany(name => signaturesByName[name]);

            // Remove the methods that use members that cannot be shared, until nothing changes.
            bool changed;
            do
            {
                changed = false;
                foreach (var signature in shared.ToArray())
                {
                    var method = methodsByClass[0][signature];
                    var usedFields = method.Identifiers.Where(allFieldNames.Contains);
                    if (!usedFields.All(IsShareableField) ||
                        !Callees(method).All(callee => shared.Contains(callee) || IsOverridable(callee)))
                    {
                        shared.Remove(signature);
                        changed = true;
                    }
                }
            }
            while (changed);

            var sharedMethodObjects = shared.Select(signature => methodsByClass[0][signature]).ToArray();
            var abstractMethods = new HashSet<string>(sharedMethodObjects.SelectMany(Callees).Where(c => !shared.Contains(c)));
            var sharedFields = new HashSet<string>(
                sharedMethodObjects.SelectMany(m => m.Identifiers).Where(allFieldNames.Contains));

            return (shared, abstractMethods, sharedFields);
        }

        static bool IsComment(string line) => line.TrimStart().StartsWith("//", StringComparison.Ordinal);

        static bool IsMemberStart(string line)
            => line.StartsWith(MemberIndent, StringComparison.Ordinal) &&
               line.Length > MemberIndent.Length &&
               line[MemberIndent.Length] != ' ';

        // The text of a class, split into its members.
        sealed class ClassText
        {
            ClassText(List<string> declarationLines, List<Member> members, List<string> trailingLines)
            {
                DeclarationLines = declarationLines;
                Members = members;
                TrailingLines = trailingLines;
            }

            // The lines before the opening brace of the class.
            internal IReadOnlyList<string> DeclarationLines { get; }

            internal IReadOnlyList<Member> Members { get; }

            // The lines after the last member, before the closing brace of the class.
            internal IReadOnlyList<string> TrailingLines { get; }

            internal static ClassText? Parse(IReadOnlyList<string> lines)
            {
                var openBrace = IndexOf(lines, "{", 0);
                if (openBrace < 1 || lines[lines.Count - 1] != "}")
                {
                    return null;
                }

                var declarationLines = lines.Take(openBrace).ToList();
                var members = new List<Member>();
                var end = lines.Count - 1;
                var segmentStart = openBrace + 1;
                var i = segmentStart;
                while (i < end)
                {
                    var line = lines[i];
                    if (line.Length == 0 || IsComment(line))
                    {
                        i++;
                        continue;
                    }

                    if (!IsMemberStart(line))
                    {
                        return null;
                    }

                    // Find the start of the comments that precede the member.
                    var codeStart = i;
                    while (codeStart > segmentStart && IsComment(lines[codeStart - 1]))
                    {
                        codeStart--;
                    }

                    int last;
                    int headerLineCount;
                    if (line.EndsWith(";", StringComparison.Ordinal))
                    {
                        // A field or an expression bodied member.
                        last = i;
                        headerLineCount = 1;
                    }
                    else
                    {
                        var bodyStart = IndexOf(lines, MemberIndent + "{", i);
                        last = IndexOf(lines, MemberIndent + "}", bodyStart);
                        if (bodyStart < 0 || last < 0 || last >= end)
                        {
                            return null;
                        }

                        headerLineCount = bodyStart - i;
                    }

                    members.Add(new Member(
                        leadingLines: lines.Skip(segmentStart).Take(codeStart - segmentStart).ToArray(),
                        codeLines: lines.Skip(codeStart).Take(last - codeStart + 1).ToArray(),
                        headerLineCount: headerLineCount));

                    i = last + 1;
                    segmentStart = i;
                }

                var trailingLines = lines.Skip(segmentStart).Take(end - segmentStart).ToList();
                return new ClassText(declarationLines, members, trailingLines);
            }

            static int IndexOf(IReadOnlyList<string> lines, string value, int start)
            {
                if (start < 0)
                {
                    return -1;
                }

                for (var i = start; i < lines.Count; i++)
                {
                    if (lines[i] == value)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        // A member of a class: a field, a method, a constructor, or an expression bodied member.
        sealed class Member
        {
            internal Member(IReadOnlyList<string> leadingLines, IReadOnlyList<string> codeLines, int headerLineCount)
            {
                LeadingLines = leadingLines;
                CodeLines = codeLines;
                CommentLines = codeLines.TakeWhile(IsComment).ToArray();
                var code = codeLines.Skip(CommentLines.Count).ToArray();
                HeaderLines = code.Take(headerLineCount).ToArray();
                var codeWithoutComments = code.Where(l => !IsComment(l)).ToArray();
                Key = string.Join("\n", codeWithoutComments);

                var header = HeaderLines[0].Substring(MemberIndent.Length);
                if (header.EndsWith(";", StringComparison.Ordinal) && !header.Contains("=>") && !header.Contains("("))
                {
                    var match = s_field.Match(header);
                    if (match.Success)
                    {
                        FieldName = match.Groups["name"].Value;
                        Declaration = header;
                        IsReadonly = match.Groups["modifiers"].Value.Contains("readonly");
                    }
                }
                else if (!header.EndsWith(";", StringComparison.Ordinal))
                {
                    var match = s_method.Match(header);
                    if (match.Success)
                    {
                        MethodName = match.Groups["name"].Value;
                        IsStatic = match.Groups["static"].Success;

                        // Methods with access modifiers (constructors, interface implementations, and
                        // the like) are part of the class, not of the shareable implementation.
                        IsShareable = match.Groups["returnType"].Value is not ("internal" or "public" or "private" or "protected");
                    }
                }

                if (MethodName is not null)
                {
                    Signature = string.Join("\n", HeaderLines);
                    var text = s_stringLiteral.Replace(string.Join("\n", codeWithoutComments.Skip(headerLineCount)), "\"\"");
                    Identifiers = new HashSet<string>(s_identifier.Matches(text).Select(m => m.Value));
                    Calls = new HashSet<string>(s_call.Matches(text).Select(m => m.Groups[1].Value));
                }
            }

            // Blank lines before the member.
            internal IReadOnlyList<string> LeadingLines { get; }

            // The comments that precede the member, followed by the member.
            internal IReadOnlyList<string> CodeLines { get; }

            internal IReadOnlyList<string> CommentLines { get; }

            internal IReadOnlyList<string> HeaderLines { get; }

            // The code of the member, without comments. Members with equal keys are identical.
            internal string Key { get; }

            internal string? FieldName { get; }

            internal string Declaration { get; } = string.Empty;

            internal bool IsReadonly { get; }

            internal string? MethodName { get; }

            // The header of the method. Identifies the method, including overloads.
            internal string? Signature { get; }

            internal bool IsStatic { get; }

            internal bool IsShareable { get; }

            internal IReadOnlyCollection<string> Identifiers { get; } = Array.Empty<string>();

            internal IReadOnlyCollection<string> Calls { get; } = Array.Empty<string>();
        }
    }
}
