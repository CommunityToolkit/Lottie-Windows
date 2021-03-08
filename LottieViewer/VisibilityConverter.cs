// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace LottieViewer
{
    // Converts bool, integer and null values into Visibility values.
    public sealed class VisibilityConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                // The value is already a boolean.
            }
            else if (value is int count)
            {
                // True if the value is != 0.
                boolValue = count != 0;
            }
            else if (value is ICollection collection)
            {
                // True if the count is != 0.
                boolValue = collection.Count != 0;
            }
            else
            {
                // True if the value is not null.
                boolValue = value is not null;
            }

            if ((string)parameter == "not")
            {
                // The "not" parameter inverts the logic.
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Only support one way binding.
            throw new NotImplementedException();
        }
    }
}
