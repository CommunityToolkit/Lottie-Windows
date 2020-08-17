// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace LottieViewer.ViewModel
{
    // An non-generic alternative to Tuple<string, string>.
    // Neede because XAML doesn't support generic type names.
    sealed class PairOfStrings
    {
        internal PairOfStrings(string item1, string item2) => (Item1, Item2) = (item1, item2);

        public string Item1 { get; }

        public string Item2 { get; }
    }
}
