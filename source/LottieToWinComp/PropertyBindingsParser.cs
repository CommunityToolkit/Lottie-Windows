// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    static class PropertyBindingsParser
    {
        // Property binding language definition
        // ====================================
        // Property bindings are lists of property-name+binding-name pairs.
        // They are specified using using a regular language (i.e. parseable
        // by a regular expression) that is designed to look similar to CSS var
        // functions. Just like CSS, the property bindings are enclosed within
        // curly braces and multiple property bindings are separated by semicolons.
        //
        // <PropertyBindings>    ::= "{" <OptWS> <PropertyBindingList> <OptWS> "}"
        // <PropertyBindingList> ::= <PropertyBinding> | <PropertyBinding> <OptWS> ";" <OptWS> <PropertyBindingList>
        // <PropertyBinding>     ::= <PropertyName> <OptWS> ":" <OptWS> "var(" <OptWS> <BindingName> <OptWS> ")"
        // <PropertyName>        ::= <Identifier>
        // <BindingName>         ::= <Identifier>
        // <OptWS>               ::= <Whitespace> <OptWS> | ""
        // <Identifier>          ::= a word starting with an alpha character.
        // ------------------------------------
        // Example: "{ color :var( Foreground);color1:var(Foreground1) ; color3:var(L33t )   }"
        // ------------------------------------
        const string PropertyNameSelector = "PropertyName";
        const string BindingNameSelector = "BindingName";

        const string PropertyBindingRegex =

            // PropertyName  followed by optional whitespace.
            @"(?<" + PropertyNameSelector + @">\D\w*)\s*" +

            // ':'           followed by optional whitespace.
            @"\:\s*" +

            // 'var('        followed by optional whitespace.
            @"var\(\s*" +

            // BindingName   followed by optional whitespace.
            @"(?<" + BindingNameSelector + @">\D\w*)\s*" +

            // ')'           followed by optional whitespace.
            @"\)\s*";

        const string PropertyBindingsListRegex =

            // At least one property binding.
            PropertyBindingRegex +

            // Optional: more property bindings separated by semicolons
            @"(;\s*" + PropertyBindingRegex + @"\s*)*";

        static readonly Regex s_regex = new Regex(@"{\s*" + PropertyBindingsListRegex + @"}");

        // Parses property bindings from the given string.
        internal static (string propertyName, string bindingName)[] ParseBindings(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return Array.Empty<(string, string)>();
            }

            var matches = s_regex.Matches(str);
            if (matches.Count == 0)
            {
                return Array.Empty<(string, string)>();
            }

            return
                (from Match match in matches
                 from pair in match.Groups[PropertyNameSelector].Captures
                                   .Zip(match.Groups[BindingNameSelector].Captures, (p, b) => (p.Value, b.Value))
                 select pair).ToArray();
        }
    }
}