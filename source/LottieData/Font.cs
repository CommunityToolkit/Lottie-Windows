// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData
{
#if PUBLIC_LottieData
    public
#endif
    sealed class Font
    {
        public Font(
            string name,
            string family,
            string style,
            double ascent)
        {
            Name = name;
            Family = family;
            Style = style;
            Ascent = ascent;
        }

        public string Name { get; }

        public string Family { get; }

        public string Style { get; }

        public double Ascent { get; }
    }
}
