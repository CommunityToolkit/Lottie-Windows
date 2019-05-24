// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIData
    public
#endif
    sealed class CodeBuilder
    {
        const int IndentSize = 4;
        readonly List<CodeLine> _lines = new List<CodeLine>();
        int _indentCount = 0;

        internal bool IsEmpty => _lines.Count == 0;

        internal void WriteLine()
        {
            WriteLine(string.Empty);
        }

        internal void WriteLine(string line)
        {
            _lines.Add(new CodeLine { Text = line, IndentCount = _indentCount });
        }

        internal void WriteComment(string comment)
        {
            if (!string.IsNullOrWhiteSpace(comment))
            {
                foreach (var line in BreakUpLine(comment))
                {
                    WriteLine($"// {line}");
                }
            }
        }

        internal void WriteCodeBuilder(CodeBuilder builder)
        {
            _lines.Add(new CodeLine { Text = builder, IndentCount = _indentCount });
        }

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

        internal void Indent()
        {
            _indentCount++;
        }

        internal void UnIndent()
        {
            Debug.Assert(_indentCount > 0, "Unmatched Indent()/UnIndent() calls");
            _indentCount--;
        }

        internal void Clear()
        {
            _lines.Clear();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return ToString(0);
        }

        internal string ToString(int indentCount)
        {
            var sb = new StringBuilder();
            foreach (var line in _lines)
            {
                var builder = line.Text as CodeBuilder;
                if (builder != null)
                {
                    sb.Append(builder.ToString(line.IndentCount + indentCount));
                }
                else
                {
                    var lineText = line.Text.ToString();
                    if (string.IsNullOrWhiteSpace(lineText))
                    {
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.Append(new string(' ', (line.IndentCount + indentCount) * IndentSize));
                        sb.AppendLine(lineText);
                    }
                }
            }

            return sb.ToString();
        }

        // Breaks up the given text into lines.
        static IEnumerable<string> BreakUpLine(string text) => BreakUpLine(text, 83);

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

        // Returns the next line from the front of the given text, ensuring it is no more
        // than maxLineLength and a tail that contains the remainder.
        static string GetLine(string text, int maxLineLength, out string remainder)
        {
            text = text.TrimEnd();

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
                                if (breakLookahead > maxLineLength)
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
        internal void BytesToLiteral(byte[] bytes, int maximumColumns)
        {
            var bytesLines = BytesToBytesList(bytes, maximumColumns - 1 - IndentSize).ToArray();

            // Write each byte line one at a time and append ',' at the end, except the last line.
            int i;
            for (i = 0; i < bytesLines.Length - 1; i++)
            {
                WriteLine($"{bytesLines[i]},");
            }

            WriteLine(bytesLines[i]);
        }

        static IEnumerable<string> BytesToBytesList(byte[] bytes, int maximumWidth)
        {
            const string delimeter = ", ";

            IEnumerable<string> byteStrings = bytes.Select(b => b.ToString());

            // Keep pulling byte strings into the current collection until the length gets too long or we run out.
            var accumulator = new List<string>();
            var currentWidth = 0;
            foreach (var bs in byteStrings)
            {
                if (currentWidth + (delimeter.Length * accumulator.Count) + bs.Length > maximumWidth)
                {
                    // There is no room for the next byte string. Output what we have.
                    yield return string.Join(delimeter, accumulator);
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
            internal object Text;
            internal int IndentCount;
        }
    }
}
