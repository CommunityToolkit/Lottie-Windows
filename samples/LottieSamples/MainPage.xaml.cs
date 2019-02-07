// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using LottieSamples.Scenarios;
using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace LottieSamples
{
    internal sealed class NavData
    {
        public string Tag;
        public Type Page;

        public static implicit operator NavData((string tag, Type page)arg)
            => new NavData {  Tag = arg.tag, Page = arg.page};
    }

    public sealed partial class MainPage : Page
    {
        // List of ValueTuple with the Navigation Tag and the corresponding Navigation Page.
        internal readonly NavData[] _pages = new NavData[]
        {
            ("json", typeof(JsonPage)),
            ("codegen", typeof(CodegenPage)),
            ("playback", typeof(PlaybackPage)),
            ("progress", typeof(ProgressPage)),
            ("segment", typeof(SegmentPage)),
            ("async", typeof(AsyncPage)),
            ("fallback", typeof(FallbackPage)),
            ("modify", typeof(ModifyPage))
        };

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigated += On_Navigated;
            NavView.SelectedItem = NavView.MenuItems[0];
            NavView_Navigate("json", new EntranceNavigationTransitionInfo());  // Default page.
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                NavView_Navigate(navItemTag, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavView_Navigate(string navItemTag, NavigationTransitionInfo transitionInfo)
        {
            var item = _pages.FirstOrDefault(p => p.Tag.Equals(navItemTag));
            var pageType = item.Page;

            if (!(pageType is null))
            {
                ContentFrame.Navigate(pageType, null, transitionInfo);
            }
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            if (ContentFrame.SourcePageType != null)
            {
                var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

                NavView.SelectedItem = NavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .First(n => n.Tag.Equals(item.Tag));

                // Consequence of substituting NavigationViewItem's Icon + Content with a Stackpanel + 2 TextBlocks. 
                NavView.Header = ((TextBlock)((StackPanel)((NavigationViewItem)NavView.SelectedItem)?.Content)?.Children[1]).Text.ToString();
            }
        }
    }
}