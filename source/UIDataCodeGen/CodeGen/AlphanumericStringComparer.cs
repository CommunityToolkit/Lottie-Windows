// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    // Defines a case-sensitive alphabetical order for strings, but treats numeric parts
    // of each string as numbers. This is useful for ordering strings that include numeric
    // qualifiers, e.g. myfile_3, myfile_11.
    sealed class AlphanumericStringComparer : IComparer<string?>
    {
        static readonly Regex s_upperHexRecognizer = new Regex(@"[0-9A-F]+");
        static readonly Regex s_lowerHexRecognizer = new Regex(@"[0-9a-f]+");

        AlphanumericStringComparer()
        {
        }

        internal static AlphanumericStringComparer Instance { get; } = new AlphanumericStringComparer();

        int IComparer<string?>.Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            // Does an ordinary comparison of the strings for ordering (stepping through each string until a difference
            // is found) unless a number is found in both strings at the same position. The numbers are parsed and their
            // values are used to determine the ordering. If the numbers are equal then it keeps comparing the characters
            // after the numbers.
            //
            // In order to recognize hex numbers embedded in strings without accidentally treating ordinary characters
            // in the string as hex, we require that hex values have a consistent case (i.e. all upper case or all lower
            // case). Even if the characters aren't actually hex values this usually results in the correct character ordering.
            //
            // Note that the recognition of numbers embedded in strings is a heuristic that can't always work perfectly,
            // however in the cases it's intended for (programming language names that follow typical patterns that
            // developers like to use) it seems better than any other ordering at yielding results that humans would expect.
            var xLength = x.Length;
            var yLength = y.Length;
            for (int xi = 0, yi = 0; xi < xLength && yi < yLength; xi++, yi++)
            {
                var chX = x[xi];
                var chY = y[yi];

                // Can both sides be interpreted as hex or decimal integers?
                if (IsHexDigit(chX) && IsHexDigit(chY))
                {
                    // Try to match as upper-case hex.
                    var matchX = s_upperHexRecognizer.Match(x, xi);
                    var matchY = s_upperHexRecognizer.Match(y, yi);
                    if (matchX.Success && matchX.Index == xi && matchY.Success && matchY.Index == yi)
                    {
                        // Both sides look like upper-case hex. Compare by value.
                        var xInt = int.Parse(x.Substring(xi, matchX.Length), NumberStyles.HexNumber);
                        var yInt = int.Parse(y.Substring(yi, matchY.Length), NumberStyles.HexNumber);
                        if (xInt == yInt)
                        {
                            // Step over the hex.
                            xi += matchX.Length - 1;
                            yi += matchY.Length - 1;
                            continue;
                        }
                        else
                        {
                            return xInt - yInt;
                        }
                    }

                    // Try and match as lower-case hex.
                    matchX = s_lowerHexRecognizer.Match(x, xi);
                    matchY = s_lowerHexRecognizer.Match(y, yi);
                    if (matchX.Success && matchX.Index == xi && matchY.Success && matchY.Index == yi)
                    {
                        // Both sides look like lower-case hex. Compare by value.
                        var xInt = int.Parse(x.Substring(xi, matchX.Length), NumberStyles.HexNumber);
                        var yInt = int.Parse(y.Substring(yi, matchY.Length), NumberStyles.HexNumber);
                        if (xInt == yInt)
                        {
                            // Step over the hex.
                            xi += matchX.Length - 1;
                            yi += matchY.Length - 1;
                            continue;
                        }
                        else
                        {
                            return xInt - yInt;
                        }
                    }
                }

                if (chX != chY)
                {
                    // Non-alphanumeric characters come before all alphanumeric characters.
                    if (char.IsLetterOrDigit(chX) ^ char.IsLetterOrDigit(chY))
                    {
                        // One character is alphanumeric and the other is not.
                        return char.IsLetterOrDigit(chX) ? 1 : -1;
                    }

                    // Letters compare by upper-case value, then by case.
                    if (char.IsLetter(chX) && char.IsLetter(chY))
                    {
                        var chXUpper = char.ToUpperInvariant(chX);
                        var chYupper = char.ToUpperInvariant(chY);
                        if (chXUpper == chYupper)
                        {
                            // Same letter, only differing by case. Lower case goes first.
                            return char.IsUpper(chX) ? 1 : -1;
                        }
                        else
                        {
                            return chXUpper - chYupper;
                        }
                    }

                    // Use ordinal comparison.
                    return chX - chY;
                }
            }

            // Got to the end of one of the strings or both of the strings and they matched so far.
            // The shorter string goes first.
            return xLength - yLength;
        }

        static bool IsHexDigit(char ch)
            => (ch >= '0' && ch <= '9')
                || (ch >= 'a' && ch <= 'f')
                || (ch >= 'A' && ch <= 'F');
    }
}
