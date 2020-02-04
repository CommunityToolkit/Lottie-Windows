// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    sealed class PropertyBindings
    {
        // Identifies the bound property names in TranslationResult.SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        readonly List<(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)> _names =
            new List<(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)>();

        // Adds the current list of property bindings to the source metatadata dictionary.
        internal void AddToSourceMetadata(Dictionary<Guid, object> sourceMetadata)
        {
            if (_names.Count > 0)
            {
                // Add the binding descriptions, ordered by binding name.
                sourceMetadata.Add(s_propertyBindingNamesKey, _names.OrderBy(n => n.Item1).ToArray());
            }
        }

        // Adds a property binding to the list of property bindings.
        internal void AddPropertyBinding(string bindingName, PropertySetValueType actualType, PropertySetValueType exposedType)
             => _names.Add((bindingName, actualType, exposedType));

        // Parses the given binding string and returns the binding name for the given property, or
        // null if not found. Returns the first matching binding name (there could be more than
        // one match).
        // This is used to retrieve property bindings from binding expressions embedded in Lottie
        // object names.
        internal static string FindFirstBindingNameForProperty(string bindingString, string propertyName)
                => PropertyBindingsParser.ParseBindings(bindingString)
                    .Where(p => p.propertyName == propertyName)
                    .Select(p => p.bindingName).FirstOrDefault();
    }
}