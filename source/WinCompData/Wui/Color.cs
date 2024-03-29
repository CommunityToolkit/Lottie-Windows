// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Wui
{
#if PUBLIC_WinCompData
    public
#endif
    readonly struct Color : IEquatable<Color>
    {
        Color(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color(a, r, g, b);

        public readonly byte A;
        public readonly byte B;
        public readonly byte G;
        public readonly byte R;

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>
        /// Gets a string that describes the color for human consumption. This either returns
        /// the well known name for the color, or a name that describes the color as the color
        /// it is nearest to along with the hex value. The name is guaranteed to be unique for
        /// any particular ARGB value, and contains only characters suitable for use in C#
        /// identifiers.
        /// </summary>
        public string Name
        {
            get
            {
                var result = GetFriendlyName(A, R, G, B);
                if (result is null)
                {
                    // Get the nearest named color.
                    result = GetNearestNamedColor(this).Name;

                    // Indicate that the color is near the name, and include the hex value
                    // so that it will be unique.
                    result = $"Almost{result}_{Hex}";

                    if (A != 255)
                    {
                        // If the color has any transparency, indicate that in the name.
                        result = A == 0 ? $"Transparent{result}" : $"SemiTransparent{result}";
                    }
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Color other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => A * R * G * B;

        public static bool operator ==(Color left, Color right) => left.Equals(right);

        public static bool operator !=(Color left, Color right) => !left.Equals(right);

        /// <summary>
        /// Attempts to get the friendly name for this color.
        /// </summary>
        /// <returns><c>true</c> if a friendly name was returned.</returns>
        public bool TryGetFriendlyName([MaybeNullWhen(false)] out string name)
        {
            name = GetFriendlyName(A, R, G, B);
            return name is not null;
        }

        /// <summary>
        /// Gets the hex representation of this color.
        /// </summary>
        public string Hex => $"{ToHex(A)}{ToHex(R)}{ToHex(G)}{ToHex(B)}";

        /// <summary>
        /// Gets the hex representation of the RGB values of this color.
        /// </summary>
        public string HexWithoutAlpha => $"{ToHex(R)}{ToHex(G)}{ToHex(B)}";

        // Gets the friendly name if one exists, or null.
        // The same name will not be returned for more than one ARGB value.
        // The result is slightly different from GetWellKnownName in that it will
        // return names for many transparent and semi-transparent values.
        static string? GetFriendlyName(byte a, byte r, byte g, byte b)
        {
            var result = GetWellKnownName(a, r, g, b);
            if (result is null)
            {
                if (a != 255)
                {
                    // The value has some transparency. Try again ignoring alpha.
                    result = GetWellKnownName(255, r, g, b);
                    if (result is not null)
                    {
                        result = a == 0 ? $"Transparent{result}" : $"SemiTransparent{result}";
                    }
                }
            }

            return result;
        }

        // Gets the well known name for a given ARGB value, or null if no name is known.
        static string? GetWellKnownName(byte a, byte r, byte g, byte b) =>
            (((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b) switch
            {
                0x00000000 => "Transparent",
                0xFF000000 => "Black",
                0xFF000080 => "Navy",
                0xFF00008B => "DarkBlue",
                0xFF0000CD => "MediumBlue",
                0xFF0000FF => "Blue",
                0xFF006400 => "DarkGreen",
                0xFF008000 => "Green",
                0xFF008080 => "Teal",
                0xFF008B8B => "DarkCyan",
                0xFF00BFFF => "DeepSkyBlue",
                0xFF00CED1 => "DarkTurquoise",
                0xFF00FA9A => "MediumSpringGreen",
                0xFF00FF00 => "Lime",
                0xFF00FF7F => "SpringGreen",

                // Aqua and Cyan have the same value.
                //case 0xFF00FFFF: return "Aqua";
                0xFF00FFFF => "Cyan",
                0xFF191970 => "MidnightBlue",
                0xFF1E90FF => "DodgerBlue",
                0xFF20B2AA => "LightSeaGreen",
                0xFF228B22 => "ForestGreen",
                0xFF2E8B57 => "SeaGreen",
                0xFF2F4F4F => "DarkSlateGray",
                0xFF32CD32 => "LimeGreen",
                0xFF3CB371 => "MediumSeaGreen",
                0xFF40E0D0 => "Turquoise",
                0xFF4169E1 => "RoyalBlue",
                0xFF4682B4 => "SteelBlue",
                0xFF483D8B => "DarkSlateBlue",
                0xFF48D1CC => "MediumTurquoise",
                0xFF4B0082 => "Indigo",
                0xFF556B2F => "DarkOliveGreen",
                0xFF5F9EA0 => "CadetBlue",
                0xFF6495ED => "CornflowerBlue",
                0xFF66CDAA => "MediumAquamarine",
                0xFF696969 => "DimGray",
                0xFF6A5ACD => "SlateBlue",
                0xFF6B8E23 => "OliveDrab",
                0xFF708090 => "SlateGray",
                0xFF778899 => "LightSlateGray",
                0xFF7B68EE => "MediumSlateBlue",
                0xFF7CFC00 => "LawnGreen",
                0xFF7FFF00 => "Chartreuse",
                0xFF7FFFD4 => "Aquamarine",
                0xFF800000 => "Maroon",
                0xFF800080 => "Purple",
                0xFF808000 => "Olive",
                0xFF808080 => "Gray",
                0xFF87CEEB => "SkyBlue",
                0xFF87CEFA => "LightSkyBlue",
                0xFF8A2BE2 => "BlueViolet",
                0xFF8B0000 => "DarkRed",
                0xFF8B008B => "DarkMagenta",
                0xFF8B4513 => "SaddleBrown",
                0xFF8FBC8F => "DarkSeaGreen",
                0xFF90EE90 => "LightGreen",
                0xFF9370DB => "MediumPurple",
                0xFF9400D3 => "DarkViolet",
                0xFF98FB98 => "PaleGreen",
                0xFF9932CC => "DarkOrchid",
                0xFF9ACD32 => "YellowGreen",
                0xFFA0522D => "Sienna",
                0xFFA52A2A => "Brown",
                0xFFA9A9A9 => "DarkGray",
                0xFFADD8E6 => "LightBlue",
                0xFFADFF2F => "GreenYellow",
                0xFFAFEEEE => "PaleTurquoise",
                0xFFB0C4DE => "LightSteelBlue",
                0xFFB0E0E6 => "PowderBlue",
                0xFFB22222 => "Firebrick",
                0xFFB8860B => "DarkGoldenrod",
                0xFFBA55D3 => "MediumOrchid",
                0xFFBC8F8F => "RosyBrown",
                0xFFBDB76B => "DarkKhaki",
                0xFFC0C0C0 => "Silver",
                0xFFC71585 => "MediumVioletRed",
                0xFFCD5C5C => "IndianRed",
                0xFFCD853F => "Peru",
                0xFFD2691E => "Chocolate",
                0xFFD2B48C => "Tan",
                0xFFD3D3D3 => "LightGray",
                0xFFD8BFD8 => "Thistle",
                0xFFDA70D6 => "Orchid",
                0xFFDAA520 => "Goldenrod",
                0xFFDB7093 => "PaleVioletRed",
                0xFFDC143C => "Crimson",
                0xFFDCDCDC => "Gainsboro",
                0xFFDDA0DD => "Plum",
                0xFFDEB887 => "BurlyWood",
                0xFFE0FFFF => "LightCyan",
                0xFFE6E6FA => "Lavender",
                0xFFE9967A => "DarkSalmon",
                0xFFEE82EE => "Violet",
                0xFFEEE8AA => "PaleGoldenrod",
                0xFFF08080 => "LightCoral",
                0xFFF0E68C => "Khaki",
                0xFFF0F8FF => "AliceBlue",
                0xFFF0FFF0 => "Honeydew",
                0xFFF0FFFF => "Azure",
                0xFFF4A460 => "SandyBrown",
                0xFFF5DEB3 => "Wheat",
                0xFFF5F5DC => "Beige",
                0xFFF5F5F5 => "WhiteSmoke",
                0xFFF5FFFA => "MintCream",
                0xFFF8F8FF => "GhostWhite",
                0xFFFA8072 => "Salmon",
                0xFFFAEBD7 => "AntiqueWhite",
                0xFFFAF0E6 => "Linen",
                0xFFFAFAD2 => "LightGoldenrodYellow",
                0xFFFDF5E6 => "OldLace",
                0xFFFF0000 => "Red",

                // Fuchsia and Magenta have the same value.
                //case 0xFFFF00FF: return "Fuchsia";
                0xFFFF00FF => "Magenta",
                0xFFFF1493 => "DeepPink",
                0xFFFF4500 => "OrangeRed",
                0xFFFF6347 => "Tomato",
                0xFFFF69B4 => "HotPink",
                0xFFFF7F50 => "Coral",
                0xFFFF8C00 => "DarkOrange",
                0xFFFFA07A => "LightSalmon",
                0xFFFFA500 => "Orange",
                0xFFFFB6C1 => "LightPink",
                0xFFFFC0CB => "Pink",
                0xFFFFD700 => "Gold",
                0xFFFFDAB9 => "PeachPuff",
                0xFFFFDEAD => "NavajoWhite",
                0xFFFFE4B5 => "Moccasin",
                0xFFFFE4C4 => "Bisque",
                0xFFFFE4E1 => "MistyRose",
                0xFFFFEBCD => "BlanchedAlmond",
                0xFFFFEFD5 => "PapayaWhip",
                0xFFFFF0F5 => "LavenderBlush",
                0xFFFFF5EE => "SeaShell",
                0xFFFFF8DC => "Cornsilk",
                0xFFFFFACD => "LemonChiffon",
                0xFFFFFAF0 => "FloralWhite",
                0xFFFFFAFA => "Snow",
                0xFFFFFF00 => "Yellow",
                0xFFFFFFE0 => "LightYellow",
                0xFFFFFFF0 => "Ivory",
                0xFFFFFFFF => "White",
                _ => null,
            };

        static string ToHex(byte value) => value.ToString("X2");

        // The named UWP colors. Ordered by distance from previous colors.
        internal static readonly (string Name, Color Color)[] Colors = new[]
        {
            (Name: "Black", Color: new Color(a:255, r:0, g:0, b:0)),
            (Name: "Blue", Color: new Color(a:255, r:0, g:0, b:255)),
            (Name: "Green", Color: new Color(a:255, r:0, g:128, b:0)),
            (Name: "Red", Color: new Color(a:255, r:255, g:0, b:0)),
            (Name: "White", Color: new Color(a:255, r:255, g:255, b:255)),
            (Name: "Aqua", Color: new Color(a:255, r:0, g:255, b:255)),
            (Name: "Cyan", Color: new Color(a:255, r:0, g:255, b:255)),
            (Name: "Fuchsia", Color: new Color(a:255, r:255, g:0, b:255)),
            (Name: "Magenta", Color: new Color(a:255, r:255, g:0, b:255)),
            (Name: "Yellow", Color: new Color(a:255, r:255, g:255, b:0)),
            (Name: "LightSlateGray", Color: new Color(a:255, r:119, g:136, b:153)),
            (Name: "Coral", Color: new Color(a:255, r:255, g:127, b:80)),
            (Name: "Indigo", Color: new Color(a:255, r:75, g:0, b:130)),
            (Name: "SaddleBrown", Color: new Color(a:255, r:139, g:69, b:19)),
            (Name: "Aquamarine", Color: new Color(a:255, r:127, g:255, b:212)),
            (Name: "LawnGreen", Color: new Color(a:255, r:124, g:252, b:0)),
            (Name: "SpringGreen", Color: new Color(a:255, r:0, g:255, b:127)),
            (Name: "Violet", Color: new Color(a:255, r:238, g:130, b:238)),
            (Name: "MediumVioletRed", Color: new Color(a:255, r:199, g:21, b:133)),
            (Name: "Lime", Color: new Color(a:255, r:0, g:255, b:0)),
            (Name: "Teal", Color: new Color(a:255, r:0, g:128, b:128)),
            (Name: "Khaki", Color: new Color(a:255, r:240, g:230, b:140)),
            (Name: "DodgerBlue", Color: new Color(a:255, r:30, g:144, b:255)),
            (Name: "BlueViolet", Color: new Color(a:255, r:138, g:43, b:226)),
            (Name: "Orange", Color: new Color(a:255, r:255, g:165, b:0)),
            (Name: "LimeGreen", Color: new Color(a:255, r:50, g:205, b:50)),
            (Name: "DarkSlateGray", Color: new Color(a:255, r:47, g:79, b:79)),
            (Name: "OliveDrab", Color: new Color(a:255, r:107, g:142, b:35)),
            (Name: "Silver", Color: new Color(a:255, r:192, g:192, b:192)),
            (Name: "DarkGoldenrod", Color: new Color(a:255, r:184, g:134, b:11)),
            (Name: "PaleVioletRed", Color: new Color(a:255, r:219, g:112, b:147)),
            (Name: "DarkKhaki", Color: new Color(a:255, r:189, g:183, b:107)),
            (Name: "DarkBlue", Color: new Color(a:255, r:0, g:0, b:139)),
            (Name: "LightSeaGreen", Color: new Color(a:255, r:32, g:178, b:170)),
            (Name: "CornflowerBlue", Color: new Color(a:255, r:100, g:149, b:237)),
            (Name: "Maroon", Color: new Color(a:255, r:128, g:0, b:0)),
            (Name: "Crimson", Color: new Color(a:255, r:220, g:20, b:60)),
            (Name: "LightGreen", Color: new Color(a:255, r:144, g:238, b:144)),
            (Name: "YellowGreen", Color: new Color(a:255, r:154, g:205, b:50)),
            (Name: "OrangeRed", Color: new Color(a:255, r:255, g:69, b:0)),
            (Name: "Turquoise", Color: new Color(a:255, r:64, g:224, b:208)),
            (Name: "MediumOrchid", Color: new Color(a:255, r:186, g:85, b:211)),
            (Name: "Pink", Color: new Color(a:255, r:255, g:192, b:203)),
            (Name: "DarkMagenta", Color: new Color(a:255, r:139, g:0, b:139)),
            (Name: "MediumSeaGreen", Color: new Color(a:255, r:60, g:179, b:113)),
            (Name: "LightSkyBlue", Color: new Color(a:255, r:135, g:206, b:250)),
            (Name: "DarkSlateBlue", Color: new Color(a:255, r:72, g:61, b:139)),
            (Name: "SlateBlue", Color: new Color(a:255, r:106, g:90, b:205)),
            (Name: "IndianRed", Color: new Color(a:255, r:205, g:92, b:92)),
            (Name: "DimGray", Color: new Color(a:255, r:105, g:105, b:105)),
            (Name: "DeepPink", Color: new Color(a:255, r:255, g:20, b:147)),
            (Name: "DarkTurquoise", Color: new Color(a:255, r:0, g:206, b:209)),
            (Name: "MediumAquamarine", Color: new Color(a:255, r:102, g:205, b:170)),
            (Name: "SteelBlue", Color: new Color(a:255, r:70, g:130, b:180)),
            (Name: "GreenYellow", Color: new Color(a:255, r:173, g:255, b:47)),
            (Name: "LightSalmon", Color: new Color(a:255, r:255, g:160, b:122)),
            (Name: "PaleTurquoise", Color: new Color(a:255, r:175, g:238, b:238)),
            (Name: "Firebrick", Color: new Color(a:255, r:178, g:34, b:34)),
            (Name: "LemonChiffon", Color: new Color(a:255, r:255, g:250, b:205)),
            (Name: "Peru", Color: new Color(a:255, r:205, g:133, b:63)),
            (Name: "DarkSeaGreen", Color: new Color(a:255, r:143, g:188, b:143)),
            (Name: "MediumBlue", Color: new Color(a:255, r:0, g:0, b:205)),
            (Name: "SeaGreen", Color: new Color(a:255, r:46, g:139, b:87)),
            (Name: "HotPink", Color: new Color(a:255, r:255, g:105, b:180)),
            (Name: "ForestGreen", Color: new Color(a:255, r:34, g:139, b:34)),
            (Name: "DeepSkyBlue", Color: new Color(a:255, r:0, g:191, b:255)),
            (Name: "MediumPurple", Color: new Color(a:255, r:147, g:112, b:219)),
            (Name: "RoyalBlue", Color: new Color(a:255, r:65, g:105, b:225)),
            (Name: "Gainsboro", Color: new Color(a:255, r:220, g:220, b:220)),
            (Name: "DarkViolet", Color: new Color(a:255, r:148, g:0, b:211)),
            (Name: "Goldenrod", Color: new Color(a:255, r:218, g:165, b:32)),
            (Name: "MidnightBlue", Color: new Color(a:255, r:25, g:25, b:112)),
            (Name: "RosyBrown", Color: new Color(a:255, r:188, g:143, b:143)),
            (Name: "Chocolate", Color: new Color(a:255, r:210, g:105, b:30)),
            (Name: "Olive", Color: new Color(a:255, r:128, g:128, b:0)),
            (Name: "DarkOliveGreen", Color: new Color(a:255, r:85, g:107, b:47)),
            (Name: "BurlyWood", Color: new Color(a:255, r:222, g:184, b:135)),
            (Name: "Gold", Color: new Color(a:255, r:255, g:215, b:0)),
            (Name: "DarkGray", Color: new Color(a:255, r:169, g:169, b:169)),
            (Name: "Wheat", Color: new Color(a:255, r:245, g:222, b:179)),
            (Name: "Plum", Color: new Color(a:255, r:221, g:160, b:221)),
            (Name: "Orchid", Color: new Color(a:255, r:218, g:112, b:214)),
            (Name: "Sienna", Color: new Color(a:255, r:160, g:82, b:45)),
            (Name: "LightSteelBlue", Color: new Color(a:255, r:176, g:196, b:222)),
            (Name: "Salmon", Color: new Color(a:255, r:250, g:128, b:114)),
            (Name: "CadetBlue", Color: new Color(a:255, r:95, g:158, b:160)),
            (Name: "Lavender", Color: new Color(a:255, r:230, g:230, b:250)),
            (Name: "MediumSlateBlue", Color: new Color(a:255, r:123, g:104, b:238)),
            (Name: "MistyRose", Color: new Color(a:255, r:255, g:228, b:225)),
            (Name: "Thistle", Color: new Color(a:255, r:216, g:191, b:216)),
            (Name: "Tomato", Color: new Color(a:255, r:255, g:99, b:71)),
            (Name: "SandyBrown", Color: new Color(a:255, r:244, g:164, b:96)),
            (Name: "DarkGreen", Color: new Color(a:255, r:0, g:100, b:0)),
            (Name: "Gray", Color: new Color(a:255, r:128, g:128, b:128)),
            (Name: "DarkOrchid", Color: new Color(a:255, r:153, g:50, b:204)),
            (Name: "MediumSpringGreen", Color: new Color(a:255, r:0, g:250, b:154)),
            (Name: "LightCyan", Color: new Color(a:255, r:224, g:255, b:255)),
            (Name: "DarkOrange", Color: new Color(a:255, r:255, g:140, b:0)),
            (Name: "DarkSalmon", Color: new Color(a:255, r:233, g:150, b:122)),
            (Name: "LightBlue", Color: new Color(a:255, r:173, g:216, b:230)),
            (Name: "Honeydew", Color: new Color(a:255, r:240, g:255, b:240)),
            (Name: "Bisque", Color: new Color(a:255, r:255, g:228, b:196)),
            (Name: "LightYellow", Color: new Color(a:255, r:255, g:255, b:224)),
            (Name: "LavenderBlush", Color: new Color(a:255, r:255, g:240, b:245)),
            (Name: "MediumTurquoise", Color: new Color(a:255, r:72, g:209, b:204)),
            (Name: "Brown", Color: new Color(a:255, r:165, g:42, b:42)),
            (Name: "PaleGreen", Color: new Color(a:255, r:152, g:251, b:152)),
            (Name: "LightCoral", Color: new Color(a:255, r:240, g:128, b:128)),
            (Name: "AliceBlue", Color: new Color(a:255, r:240, g:248, b:255)),
            (Name: "LightGray", Color: new Color(a:255, r:211, g:211, b:211)),
            (Name: "DarkCyan", Color: new Color(a:255, r:0, g:139, b:139)),
            (Name: "Purple", Color: new Color(a:255, r:128, g:0, b:128)),
            (Name: "PaleGoldenrod", Color: new Color(a:255, r:238, g:232, b:170)),
            (Name: "Ivory", Color: new Color(a:255, r:255, g:255, b:240)),
            (Name: "SkyBlue", Color: new Color(a:255, r:135, g:206, b:235)),
            (Name: "Beige", Color: new Color(a:255, r:245, g:245, b:220)),
            (Name: "LightPink", Color: new Color(a:255, r:255, g:182, b:193)),
            (Name: "SlateGray", Color: new Color(a:255, r:112, g:128, b:144)),
            (Name: "PapayaWhip", Color: new Color(a:255, r:255, g:239, b:213)),
            (Name: "Tan", Color: new Color(a:255, r:210, g:180, b:140)),
            (Name: "PeachPuff", Color: new Color(a:255, r:255, g:218, b:185)),
            (Name: "Linen", Color: new Color(a:255, r:250, g:240, b:230)),
            (Name: "NavajoWhite", Color: new Color(a:255, r:255, g:222, b:173)),
            (Name: "WhiteSmoke", Color: new Color(a:255, r:245, g:245, b:245)),
            (Name: "DarkRed", Color: new Color(a:255, r:139, g:0, b:0)),
            (Name: "Navy", Color: new Color(a:255, r:0, g:0, b:128)),
            (Name: "Moccasin", Color: new Color(a:255, r:255, g:228, b:181)),
            (Name: "MintCream", Color: new Color(a:255, r:245, g:255, b:250)),
            (Name: "BlanchedAlmond", Color: new Color(a:255, r:255, g:235, b:205)),
            (Name: "SeaShell", Color: new Color(a:255, r:255, g:245, b:238)),
            (Name: "PowderBlue", Color: new Color(a:255, r:176, g:224, b:230)),
            (Name: "Cornsilk", Color: new Color(a:255, r:255, g:248, b:220)),
            (Name: "GhostWhite", Color: new Color(a:255, r:248, g:248, b:255)),
            (Name: "LightGoldenrodYellow", Color: new Color(a:255, r:250, g:250, b:210)),
            (Name: "Snow", Color: new Color(a:255, r:255, g:250, b:250)),
            (Name: "Azure", Color: new Color(a:255, r:240, g:255, b:255)),
            (Name: "AntiqueWhite", Color: new Color(a:255, r:250, g:235, b:215)),
            (Name: "OldLace", Color: new Color(a:255, r:253, g:245, b:230)),
            (Name: "FloralWhite", Color: new Color(a:255, r:255, g:250, b:240)),
            (Name: "Chartreuse", Color: new Color(a:255, r:127, g:255, b:0)),
            (Name: "Transparent", Color: new Color(a:0, r:255, g:255, b:255)),
        };

        static (string Name, Color Color) GetNearestNamedColor(Color color)
        {
            return
                (from namedColor in Colors
                 let distance = GetDistanceSquaredBetweenColors(color, namedColor.Color)
                 orderby distance
                 select namedColor).First();
        }

        // Returns the square of the distance between 2 colors. This is useful for finding
        // the nearest named color.
        // Ignores the alpha channel.
        internal static int GetDistanceSquaredBetweenColors(Color a, Color b)
        {
            var rDiff = a.R - b.R;
            var gDiff = a.G - b.G;
            var bDiff = a.B - b.B;

            return (rDiff * rDiff) + (gDiff * gDiff) + (bDiff * bDiff);
        }
    }
}
#if GenerateColorInfo
namespace CommunityToolkit.WinUI.Lottie
{
    using CommunityToolkit.WinUI.Lottie.WinCompData.Wui;

    // In case it's ever needed again, this code helps classify colors in various ways.
    public sealed class Color
    {
        public static void DoStuff()
        {
            PrintColorsInOrderOfOrthogonality();
        }

        static void PrintColor(string name, Color color, int orthogonality)
        {
            System.Diagnostics.Debug.WriteLine($"(Name: \"{name}\", Color: new Color(a:{color.A}, r:{color.R}, g:{color.G}, b:{color.B})),");
        }

        static void PrintColorsInOrderOfOrthogonality()
        {
            var initialColors = Color.Colors.Where(pair =>
                pair.Name == "Black" ||
                pair.Name == "White" ||
                pair.Name == "Red" ||
                pair.Name == "Green" ||
                pair.Name == "Blue");

            int orthogonality = 0;


            foreach (var item in initialColors)
            {
                PrintColor(item.Name, item.Color, orthogonality);
            }

            while (true)
            {
                orthogonality++;
                var next = GetMostOrthogonalColors(initialColors);
                if (!next.Any())
                {
                    break;
                }

                foreach (var item in next)
                {
                    PrintColor(item.Name, item.Color, orthogonality);
                }
                initialColors = initialColors.Concat(next);
            }
        }

        // Returns the colors that have the furthest distance in RGB space from the given list of colors.
        static System.Collections.Generic.IEnumerable<(string Name, Color Color)> GetMostOrthogonalColors(
                    System.Collections.Generic.IEnumerable<(string Name, Color Color)> existingColors)
        {
            // Remove the colors that are already in the existingColors set.
            var palette = Color.Colors.Except(existingColors);

            if (!palette.Any())
            {
                return Array.Empty<(string Name, Color Color)>();
            }

            return
                (from item in palette
                     // Find the closest distance to any color in the list.
                 let distance = (from existingColor in existingColors
                                 select Color.GetDistanceSquaredBetweenColors(item.Color, existingColor.Color)).Min()
                 // Order such that the color that is furthest from any color in the list will be first.
                 orderby distance descending
                 // Group colors with the same distance.
                 group item by distance into itemsByDistance
                 select itemsByDistance).First();
        }

        //// Returns all of the colors defined by Windows.UI.Colors, as name+value pairs.
        //static System.Collections.Generic.IEnumerable<(string Name, Windows.UI.Color Color)> GetUwpColors()
        //{
        //    var colorsType = typeof(Windows.UI.Colors);
        //    var colorProperties = colorsType.GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        //    foreach (var cp in colorProperties)
        //    {
        //        yield return (cp.Name, (Windows.UI.Color)colorsType.InvokeMember(cp.Name, System.Reflection.BindingFlags.GetProperty, null, null, null));
        //    }
        //}
    }
}
#endif // GenerateColorInfo
