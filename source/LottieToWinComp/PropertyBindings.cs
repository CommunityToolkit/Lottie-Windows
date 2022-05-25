// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.CompMetadata;

namespace CommunityToolkit.WinUI.Lottie.LottieToWinComp
{
    sealed class PropertyBindings
    {
        // Identifies the bound property names in TranslationResult.SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        readonly List<PropertyBinding> _propertyBindings = new List<PropertyBinding>();

        // Adds the current list of property bindings to the source metadata dictionary.
        internal void AddToSourceMetadata(Dictionary<Guid, object> sourceMetadata)
        {
            if (_propertyBindings.Count > 0)
            {
                // Add the binding descriptions, ordered by binding name.
                sourceMetadata.Add(s_propertyBindingNamesKey, _propertyBindings.OrderBy(n => n.BindingName).ToArray());
            }
        }

        // Adds a property binding to the list of property bindings.
        internal void AddPropertyBinding(PropertyBinding propertyBinding) => _propertyBindings.Add(propertyBinding);

        // Parses the given binding string and returns the binding name for the given property, or
        // null if not found. Returns the first matching binding name (there could be more than
        // one match).
        // This is used to retrieve property bindings from binding expressions embedded in Lottie
        // object names.
        internal static string? FindFirstBindingNameForProperty(string bindingString, string propertyName)
                => PropertyBindingsParser.ParseBindings(bindingString)
                    .Where(p => p.propertyName == propertyName)
                    .Select(p => p.bindingName).FirstOrDefault();
    }
}