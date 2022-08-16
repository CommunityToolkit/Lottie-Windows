// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CodeBuilder
    {
        const int IndentSize = 4;
        const int LineBreakWidth = 83;
        readonly List<CodeLine> _lines = new List<CodeLine>();
        readonly SortedDictionary<string, CodeBuilder> _subBuilders = new SortedDictionary<string, CodeBuilder>();
        int _indentCount = 0;

        /// <summary>
        /// True iff no lines have been added to this <see cref="CodeBuilder"/>.
        /// </summary>
        internal bool IsEmpty => _lines.Count == 0;

        /// <summary>
        /// The number of lines added to this <see cref="CodeBuilder"/> including the
        /// lines added to any nested <see cref="CodeBuilder"/>s.
        /// </summary>
        internal int LineCount => _lines.Sum(line => line.LineCount);

        internal void WriteLine()
        {
            WriteLine(string.Empty);
        }

        internal void WriteLine(string line)
        {
            _lines.Add(new CodeLine { Contents = line, IndentCount = _indentCount });
        }

        // Writes a line, or multiple lines if the line would be too long as a single line.
        // Typically used for writing method calls and signature.
        internal void WriteBreakableLine(string prefix, string[] breakableParts, string postfix)
        {
            // See if the content fits on a single line with a space between each breakable part.
            if (prefix.Length + /*space:*/ 1 +
                breakableParts.Sum(s => s.Length) + /*spaces:*/ breakableParts.Length +
                postfix.Length <= LineBreakWidth)
            {
                WriteLine($"{prefix}{string.Join(' ', breakableParts)}{postfix}");
            }
            else
            {
                WriteLine(prefix.TrimEnd());
                Indent();
                for (var i = 0; i < breakableParts.Length - 1; i++)
                {
                    WriteLine(breakableParts[i]);
                }

                WriteLine($"{breakableParts[^1]}{postfix.TrimStart()}");
                UnIndent();
            }
        }

        internal void WriteCommaSeparatedLines(string initialItem, IEnumerable<string> remainingItems)
        {
            WriteLine($"{initialItem},");
            WriteCommaSeparatedLines(remainingItems);
        }

        internal void WriteCommaSeparatedLines(IEnumerable<string> items)
        {
            var itemsToWrite = items.ToArray();
            for (var i = 0; i < itemsToWrite.Length; i++)
            {
                if (i < itemsToWrite.Length - 1)
                {
                    // Append "," to each item except the last one.
                    WriteLine($"{itemsToWrite[i]},");
                }
                else
                {
                    WriteLine($"{itemsToWrite[i]}");
                }
            }
        }

        internal void WriteComment(string? comment)
        {
            if (!string.IsNullOrWhiteSpace(comment))
            {
                WritePreformattedCommentLines(BreakUpLine(comment));
            }
        }

        internal void WritePreformattedCommentLine(string line) => WriteLine($"// {line}");

        internal void WritePreformattedCommentLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                WritePreformattedCommentLine(line);
            }
        }

        internal void WriteSummaryComment(string? comment)
        {
            if (!string.IsNullOrWhiteSpace(comment))
            {
                WriteLine($"/// <summary>");
                foreach (var line in BreakUpLine(comment))
                {
                    WriteLine($"/// {line}");
                }

                WriteLine($"/// </summary>");
            }
        }

        internal void WriteCodeBuilder(CodeBuilder builder)
        {
            _lines.Add(new CodeLine { Contents = builder, IndentCount = _indentCount });
        }

        /// <summary>
        /// Writes the contents of the given <see cref="CodeBuilder"/> and retains
        /// it for later access using the given key.
        /// </summary>
        internal void WriteSubBuilder(string key)
        {
            var builder = new CodeBuilder();
            WriteCodeBuilder(builder);
            _subBuilders.Add(key, builder);
        }

        internal CodeBuilder GetSubBuilder(string key)
            => _subBuilders[key];

        internal void OpenScope()
        {
            WriteLine("{");
            Indent();
        }

        internal void CloseScope()
        {
            UnIndent();
            WriteLine("}");
        }

        internal void CloseScopeWithSemicolon()
        {
            UnIndent();
            WriteLine("};");
        }

        internal void Indent()
        {
            _indentCount++;
        }

        internal void UnIndent()
        {
            _indentCount--;
            Debug.Assert(_indentCount >= 0, "Postcondition");
        }

        internal void Clear()
        {
            _lines.Clear();
        }

        /// <inheritdoc/>
        public override string ToString()
            => ToString(0);

        internal string ToString(int indentCount)
        {
            var sb = new StringBuilder();

            foreach (var line in ToLines(indentCount))
            {
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        internal IEnumerable<string> ToLines(int indentCount)
        {
            var indentString = string.Empty;

            foreach (var line in _lines)
            {
                if (line.Contents is CodeBuilder builder)
                {
                    // A nested code builder. Yield each line from it with the appropriate indent.
                    foreach (var subLineText in builder.ToLines(line.IndentCount + indentCount))
                    {
                        yield return subLineText;
                    }
                }
                else
                {
                    // A regular line.
                    var lineText = line.Contents.ToString();

                    if (string.IsNullOrWhiteSpace(lineText))
                    {
                        // An empty line.
                        yield return string.Empty;
                    }
                    else
                    {
                        // Calculate how big the indent should be, and adjust the indent
                        // string if it has changed.
                        var indentSpaceCount = (line.IndentCount + indentCount) * IndentSize;

                        if (indentSpaceCount <= 0)
                        {
                            // This fixes up any mismatch of indents. It will probably result
                            // in the output looking wrong, but that's better than crashing.
                            indentSpaceCount = 0;
                        }

                        if (indentString.Length != indentSpaceCount)
                        {
                            // The indent changed. Create a new indent string.
                            indentString = new string(' ', indentSpaceCount);
                        }

                        yield return indentString + lineText;
                    }
                }
            }
        }

        // Breaks up the given text into lines.
        static IEnumerable<string> BreakUpLine(string text) => BreakUpLine(text, LineBreakWidth);

        // Breaks up the given text into lines of at most maxLineLength characters.
        static IEnumerable<string> BreakUpLine(string text, int maxLineLength)
        {
            var rest = text;
            while (rest.Length > 0)
            {
                yield return GetLine(rest, maxLineLength, out string tail);
                rest = tail;
            }
        }

        // Returns the next line from the front of the given text, ensuring all leading and trailing whitespace
        // characters are removed, it is no more than maxLineLength and a tail that contains the remainder.
        static string GetLine(string text, int maxLineLength, out string remainder)
        {
            text = text.Trim();

            // Look for the next 2 places to break. If the 2nd place makes the line too long,
            // break at the 1st place, otherwise keep looking.
            int breakAt;
            int breakLookahead = 0;
            do
            {
                // Find the next breakable position starting at the last break point
                breakAt = breakLookahead;
                breakLookahead++;
                while (breakLookahead < text.Length)
                {
                    var cur = text[breakLookahead];
                    switch (cur)
                    {
                        // Special handling for XML markup. If a < is found, prevent breaking
                        // until the closing >.
                        case '<':
                            while (breakLookahead + 1 < text.Length)
                            {
                                breakLookahead++;
                                switch (text[breakLookahead])
                                {
                                    case '>':
                                        // Actual close found.
                                        goto XMLCLOSEFOUND;
                                    case '\r':
                                    case '\n':
                                        // It's not valid XML and the end of line was found.
                                        breakLookahead--;
                                        goto XMLCLOSEFOUND;
                                }
                            }

                        XMLCLOSEFOUND:
                            break;
                        case '\r':
                            // CR found. Break immediately
                            if (breakLookahead + 1 < text.Length && text[breakLookahead + 1] == '\n')
                            {
                                // CRLF pair - step over both
                                if (breakLookahead > maxLineLength && breakAt != 0)
                                {
                                    // Breaking at the end of the line makes the line too long. Break earlier.
                                    remainder = text.Substring(breakAt);
                                    return text.Substring(0, breakAt);
                                }
                                else
                                {
                                    // Jump over the CRLF.
                                    remainder = text.Substring(breakLookahead + 2);
                                    return text.Substring(0, breakLookahead);
                                }
                            }
                            else
                            {
                                goto case '\n';
                            }

                        case '\n':
                            // LF found. Break immediately
                            if (breakLookahead > maxLineLength)
                            {
                                remainder = text.Substring(breakAt);
                                return text.Substring(0, breakAt);
                            }
                            else
                            {
                                // Jump over the LF
                                remainder = text.Substring(breakLookahead + 1);
                                return text.Substring(0, breakLookahead);
                            }

                        default:
                            if (char.IsWhiteSpace(cur))
                            {
                                // Found the next whitespace
                                goto WHITESPACE_FOUND;
                            }

                            break;
                    }

                    breakLookahead++;
                }

            // Found whitespace or end of string. Look for next
            WHITESPACE_FOUND:
                ;
            } while (breakLookahead != text.Length && breakLookahead <= maxLineLength);

            // If no progress was made, allow the line to be too long.
            if (breakAt == 0)
            {
                breakAt = breakLookahead;
            }

            // If the breakLookahead still is less than the maximum length, return the whole
            // line.
            if (breakLookahead <= maxLineLength)
            {
                breakAt = breakLookahead;
            }

            remainder = text.Substring(breakAt);
            return text.Substring(0, breakAt);
        }

        /// <summary>
        /// Write bytes array as string literals.
        /// </summary>
        /// <param name="bytes">Bytes to be converted to string literals.</param>
        /// <param name="maximumColumns">Width limit of the output byte line.</param>
        internal void WriteByteArrayLiteral(IEnumerable<byte> bytes, int maximumColumns)
        {
            var bytesLines = BytesToBytesList(bytes, maximumColumns - 1 - (_indentCount * IndentSize)).ToArray();

            // Write each byte line one at a time and append ',' at the end, except the last line.
            int i;
            for (i = 0; i < bytesLines.Length - 1; i++)
            {
                WriteLine($"{bytesLines[i]},");
            }

            WriteLine(bytesLines[i]);
        }

        /// <summary>
        /// Write long string literal on separate lines.
        /// </summary>
        /// <param name="value">String to write.</param>
        /// <param name="maximumColumns">Width limit of the output line.</param>
        internal void WriteLongStringLiteral(string value, int maximumColumns)
        {
            int start = 0;
            int len = maximumColumns - 1 - (_indentCount * IndentSize);
            while (start < value.Length)
            {
                int end = Math.Min(start + len, value.Length);
                if (end == value.Length)
                {
                    WriteLine($"\"{value.Substring(start, end - start)}\"");
                }
                else
                {
                    WriteLine($"\"{value.Substring(start, end - start)}\" +");
                }

                start += len;
            }
        }

        static IEnumerable<string> BytesToBytesList(IEnumerable<byte> bytes, int maximumWidth)
        {
            const string delimiter = ", ";

            IEnumerable<string> byteStrings = bytes.Select(b => b.ToString());

            // Keep pulling byte strings into the current collection until the length gets too long or we run out.
            var accumulator = new List<string>();
            var currentWidth = 0;
            foreach (var bs in byteStrings)
            {
                if (currentWidth + (delimiter.Length * accumulator.Count) + bs.Length > maximumWidth)
                {
                    // There is no room for the next byte string. Output what we have.
                    yield return string.Join(delimiter, accumulator);
                    accumulator.Clear();
                    currentWidth = 0;
                }

                accumulator.Add(bs.ToString());
                currentWidth += bs.Length;
            }

            // If there are any bytes left over, output them now.
            if (accumulator.Count > 0)
            {
                yield return string.Join(", ", accumulator);
            }
        }

        struct CodeLine
        {
            // A string or a CodeBuilder.
            internal object Contents;
            internal int IndentCount;

            internal int LineCount => Contents is CodeBuilder nestedCodeBuilder ? nestedCodeBuilder.LineCount : 1;

            // In the debugger, show the contents of the line.
            public override string ToString() => Contents?.ToString() ?? string.Empty;
        }
    }
}
