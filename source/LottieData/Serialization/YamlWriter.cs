// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed class YamlWriter
    {
        const int _maximumWidth = 120;
        const int _indentSize = 3;
        readonly TextWriter _writer;
        int _column;
        int _indentColumn;
        int _inlineColumn;

        internal YamlWriter(TextWriter writer)
        {
            _writer = writer;
        }

        internal void Write(string value)
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

        internal void WriteObject(YamlObject obj)
        {
            if (obj == null)
            {
                WriteInline("~");
            }
            else
            {
                switch (obj.Kind)
                {
                    case YamlObjectKind.Scalar:
                        WriteScalar((YamlScalar)obj);
                        break;
                    case YamlObjectKind.Map:
                        WriteMap((YamlMap)obj);
                        break;
                    case YamlObjectKind.Sequence:
                        WriteSequence((YamlSequence)obj);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        void WriteScalar(YamlScalar obj)
        {
            if (TryInlineScalar(obj, _maximumWidth - _indentColumn, out var inlined))
            {
                WriteInline(inlined);
            }
            else
            {
                Write(obj.ToString());
            }
        }

        void WriteMap(YamlMap obj)
        {
            if (TryInlineMap(obj, _maximumWidth - _indentColumn, out var inlined))
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
                    Write($"{key}:");
                    _indentColumn += _indentSize;
                    WriteObject(value);
                    _indentColumn -= _indentSize;
                }

                _inlineColumn = oldInlineColumn;
            }
        }

        void WriteSequence(YamlSequence obj)
        {
            if (TryInlineSequence(obj, _maximumWidth - _indentColumn, out var inlined))
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
                    _indentColumn += _indentSize;
                    _inlineColumn = _indentColumn;
                    WriteObject(item);
                    _indentColumn -= _indentSize;
                }

                _inlineColumn = oldInlineColumn;
            }
        }

        bool TryInlineObject(YamlObject obj, int maximumWidth, out string result)
        {
            if (maximumWidth < 1)
            {
                result = null;
                return false;
            }
            else if (obj == null)
            {
                result = "~";
                return true;
            }
            else
            {
                switch (obj.Kind)
                {
                    case YamlObjectKind.Scalar:
                        return TryInlineScalar((YamlScalar)obj, maximumWidth, out result);
                    case YamlObjectKind.Map:
                        return TryInlineMap((YamlMap)obj, maximumWidth, out result);
                    case YamlObjectKind.Sequence:
                        return TryInlineSequence((YamlSequence)obj, maximumWidth, out result);
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        bool TryInlineScalar(YamlScalar obj, int maximumWidth, out string result)
        {
            result = obj.ToString();
            if (result.Length > maximumWidth)
            {
                result = null;
            }

            return result != null;
        }

        bool TryInlineMap(YamlMap obj, int maximumWidth, out string result)
        {
            result = null;
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

        bool TryInlineSequence(YamlSequence obj, int maximumWidth, out string result)
        {
            result = null;
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
    }
}