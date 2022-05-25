// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;

namespace LottieSamples.Scenarios
{
    public sealed partial class ModifyPage : Page
    {
        public ModifyPage()
        {
            this.InitializeComponent();

            // Set the colors based on system resources.
            Modified_Source_LottieLogo1.BackgroundColor = (Color)Resources["SystemBaseHighColor"];
            Modified_Source_LottieLogo1.HighlightColor = (Color)Resources["SystemAccentColor"];
            Modified_Source_LottieLogo1.TextColor = (Color)Resources["SystemAltHighColor"];

            var settings = new UISettings();
            settings.ColorValuesChanged += ModifyPage_ColorValuesChanged;
        }

        // React to system resource color changes.
        private async void ModifyPage_ColorValuesChanged(UISettings sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Modified_Source_LottieLogo1.BackgroundColor = (Color)Resources["SystemBaseHighColor"];
                    Modified_Source_LottieLogo1.HighlightColor = (Color)Resources["SystemAccentColor"];
                    Modified_Source_LottieLogo1.TextColor = (Color)Resources["SystemAltHighColor"];
                }
            );
        }
    }
}