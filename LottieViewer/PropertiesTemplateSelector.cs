// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using LottieViewer.ViewModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LottieViewer
{
    public sealed class PropertiesTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? Normal { get; set; }

        public DataTemplate? Marker { get; set; }

        public DataTemplate? MarkerWithDuration { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is PairOfStrings)
            {
                return Normal;
            }
            else if (item is Marker)
            {
                return Marker;
            }
            else
            {
                return MarkerWithDuration;
            }
        }
    }
}
