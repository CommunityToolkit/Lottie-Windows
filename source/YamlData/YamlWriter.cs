// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.YamlData
{
    /// <summary>
    /// Serializes a Yaml object into a <see cref="TextWriter"/>.
    /// </summary>
#if PUBLIC_YamlData
    public
#endif
    sealed class YamlWriter
    {
        const int _maximumWidth = 120;
        const int _indentSize = 3;
        readonly TextWriter _writer;
        int _column;
        int _indentColumn;
        int _inlineColumn;

        public YamlWriter(TextWriter writer)
        {
            _writer = writer;
        }

        /// <summary>
        /// Writes the given object to the output.
        /// </summary>
        /// <param name="obj">The object to write.</param>
        public void WriteObject(YamlObject obj)
            => WriteObject(obj, allowInlining: true);

        /// <summary>
        /// Writes the given string to the output.
        /// </summary>
        /// <param name="value">The string to write.</param>
        public void Write(string value)
        {
            if (_indentColumn > _column)
            {
                _writer.Write(new string(' ', _indentColumn - _column));
                _column = _indentColumn;
            }

            _writer.Write(value);
            _column += value.Length;
        }

        void WriteInline(string value)
        {
            if (_inlineColumn > _column)
            {
                _writer.Write(new string(' ', _inlineColumn - _column));
                _column = _inlineColumn;
            }

            _writer.Write(value);
            _column += value.Length;
        }

        void WriteLine()
        {
            _writer.WriteLine();
            _column = 0;
        }

        void WriteObject(YamlObject obj, bool allowInlining)
        {
            if (obj is null)
            {
                // Nulls are always written inline.
                WriteInline("~");
            }
            else
            {
                switch (obj.Kind)
                {
                    case YamlObjectKind.Scalar:
                        WriteScalar((YamlScalar)obj, allowInlining);
                        break;
                    case YamlObjectKind.Map:
                        WriteMap((YamlMap)obj, allowInlining);
                        break;
                    case YamlObjectKind.Sequence:
                        WriteSequence((YamlSequence)obj, allowInlining);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        void WriteScalar(YamlScalar obj, bool allowInlining)
        {
            if (allowInlining && TryInlineScalar(obj, _maximumWidth - _indentColumn, out var inlined))
            {
                WriteInline(inlined);
            }
            else
            {
                Write(obj.ToString());
            }
        }

        void WriteMap(YamlMap obj, bool allowInlining)
        {
            if (allowInlining && TryInlineMap(obj, _maximumWidth - _indentColumn, out var inlined))
            {
                WriteInline(inlined);
            }
            else
            {
                var maxKeyWidth = obj.Select(item => item.key.Length).Max();
                var oldInlineColumn = _inlineColumn;
                _inlineColumn = _indentColumn + maxKeyWidth + 2;

                foreach (var (key, value) in obj)
                {
                    WriteLine();

                    if (TryWriteComment(value))
                    {
                        WriteLine();
                        allowInlining = false;
                    }
                    else
                    {
                        allowInlining = true;
                    }

                    Write($"{key}: ");
                    _indentColumn += _indentSize;
                    WriteObject(value, allowInlining);
                    _indentColumn -= _indentSize;
                }

                _inlineColumn = oldInlineColumn;
            }
        }

        void WriteSequence(YamlSequence obj, bool allowInlining)
        {
            if (allowInlining && TryInlineSequence(obj, _maximumWidth - _indentColumn, out var inlined))
            {
                WriteInline(inlined);
            }
            else
            {
                var oldInlineColumn = _inlineColumn;
                _inlineColumn = _indentColumn + _indentSize;

                foreach (var item in obj)
                {
                    WriteLine();
                    Write("- ");

                    allowInlining = !TryWriteComment(item);

                    _indentColumn += _indentSize;
                    _inlineColumn = _indentColumn;
                    WriteObject(item, allowInlining);
                    _indentColumn -= _indentSize;
                }

                _inlineColumn = oldInlineColumn;
            }
        }

        bool TryInlineObject(YamlObject obj, int maximumWidth, [MaybeNullWhen(false)] out string result)
        {
            if (maximumWidth < 1)
            {
                result = null;
                return false;
            }
            else if (obj is null)
            {
                result = "~";
                return true;
            }
            else
            {
                switch (obj.Kind)
                {
                    case YamlObjectKind.Scalar: return TryInlineScalar((YamlScalar)obj, maximumWidth, out result);
                    case YamlObjectKind.Map: return TryInlineMap((YamlMap)obj, maximumWidth, out result);
                    case YamlObjectKind.Sequence: return TryInlineSequence((YamlSequence)obj, maximumWidth, out result);
                    default: throw new InvalidOperationException();
                }
            }
        }

        bool TryInlineScalar(YamlScalar obj, int maximumWidth, [MaybeNullWhen(false)] out string result)
        {
            result = obj.ToString();
            if (result.Length > maximumWidth)
            {
                result = null;
            }

            return result != null;
        }

        bool TryInlineMap(YamlMap obj, int maximumWidth, [MaybeNullWhen(false)] out string result)
        {
            result = null;

            // If any of the items have comments, do not inline as there's nowhere to write the comments.
            if (obj.Any(a => a.value?.Comment is string))
            {
                return false;
            }

            var sb = new StringBuilder();
            sb.Append("{ ");
            var firstSeen = false;

            foreach (var (key, value) in obj)
            {
                if (firstSeen)
                {
                    sb.Append(", ");
                }

                firstSeen = true;

                sb.Append(key);
                sb.Append(": ");
                if (TryInlineObject(value, maximumWidth - sb.Length, out var valueInlined))
                {
                    sb.Append(valueInlined);
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            sb.Append(firstSeen ? " }" : "}");
            if (sb.Length <= maximumWidth)
            {
                result = sb.ToString();
            }

            return result != null;
        }

        bool TryInlineSequence(YamlSequence obj, int maximumWidth, [MaybeNullWhen(false)] out string result)
        {
            result = null;

            // If any of the items have comments, do not inline as there's nowhere to write the comments.
            if (obj.Any(a => a?.Comment is string))
            {
                return false;
            }

            var sb = new StringBuilder();
            sb.Append("[ ");
            var firstSeen = false;

            foreach (var value in obj)
            {
                if (firstSeen)
                {
                    sb.Append(", ");
                }

                firstSeen = true;

                if (TryInlineObject(value, maximumWidth - sb.Length, out var valueInlined))
                {
                    sb.Append(valueInlined);
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            sb.Append(firstSeen ? " ]" : "]");
            if (sb.Length <= maximumWidth)
            {
                result = sb.ToString();
            }

            return result != null;
        }

        bool TryWriteComment(YamlObject obj)
        {
            if (obj?.Comment is null)
            {
                return false;
            }

            // We only support single line comments. Filter out newlines and other
            // non-printable characters.
            var cleanComment = new string(FilterNonPrintingCharacters(obj.Comment).ToArray());

            Write($"# {cleanComment}");
            return true;
        }

        static IEnumerable<char> FilterNonPrintingCharacters(string input)
        {
            var consecutiveSpaces = 0;
            foreach (var ch in input)
            {
                if (char.IsControl(ch))
                {
                    // Replace the non-printing character with a space.
                    // If a space was output previously, just drop the character
                    // on the floor.
                    if (consecutiveSpaces == 0)
                    {
                        yield return ' ';
                        consecutiveSpaces++;
                    }

                    continue;
                }
                else if (char.IsWhiteSpace(ch))
                {
                    // Keep track of the fact that some whitespace was seen.
                    consecutiveSpaces++;
                }
                else
                {
                    consecutiveSpaces = 0;
                }

                yield return ch;
            }
        }
    }
}