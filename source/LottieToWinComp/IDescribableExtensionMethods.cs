// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Extension methods for <see cref="IDescribable"/>. These make it easier to access the
    /// members of the interface without requiring a cast, and they add some debug checks
    /// to help ensure correct usage.
    /// </summary>
    static class IDescribableExtensionMethods
    {
        /// <summary>
        /// Sets a name on an object. This allows the code generator to give the object
        /// a more meaningful name.
        /// </summary>
        internal static void SetName(
            this IDescribable obj,
            string name)
        {
            Debug.Assert(obj.Name is null, "Names should never get set more than once.");
            obj.Name = name;
        }

        /// <summary>
        /// Sets a description on an object.
        /// </summary>
        internal static void SetDescription(
            this IDescribable obj,
            TranslationContext context,
            string longDescription,
            string? shortDescription = null)
        {
            Debug.Assert(context.AddDescriptions, "Descriptions should only be set when requested.");
            Debug.Assert(obj.ShortDescription is null, "Descriptions should never get set more than once.");
            Debug.Assert(obj.LongDescription is null, "Descriptions should never get set more than once.");

            obj.ShortDescription = shortDescription ?? longDescription;
            obj.LongDescription = longDescription;
        }

        /// <summary>
        /// Sets a description on an object.
        /// </summary>
        internal static void SetDescription(
            this IDescribable obj,
            TranslationContext context,
            Func<string> describer)
        {
            if (context.AddDescriptions)
            {
                var longDescription = describer();
                obj.SetDescription(context, longDescription, null);
            }
        }

        /// <summary>
        /// Sets a description on an object.
        /// </summary>
        internal static void SetDescription(
            this IDescribable obj,
            TranslationContext context,
            Func<(string longDescription, string shortDescription)> describer)
        {
            if (context.AddDescriptions)
            {
                var (longDescription, shortDescription) = describer();
                obj.SetDescription(context, longDescription, shortDescription);
            }
        }
    }
}
