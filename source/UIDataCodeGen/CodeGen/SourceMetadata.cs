// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.CompMetadata;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class SourceMetadata
    {
        // Identifies the bound property names in SourceMetadata.
        static readonly Guid s_propertyBindingNamesKey = new Guid("A115C46A-254C-43E6-A3C7-9DE516C3C3C8");

        // Identifies the Lottie metadata in SourceMetadata.
        static readonly Guid s_lottieMetadataKey = new Guid("EA3D6538-361A-4B1C-960D-50A6C35563A5");

        readonly IReadOnlyDictionary<Guid, object> _sourceMetadata;
        IReadOnlyList<PropertyBinding>? _propertyBindings;

        internal SourceMetadata(IReadOnlyDictionary<Guid, object> sourceMetadata)
        {
            _sourceMetadata = sourceMetadata;
            LottieMetadata = _sourceMetadata.TryGetValue(s_lottieMetadataKey, out var result)
                                                        ? (LottieCompositionMetadata)result
                                                        : LottieCompositionMetadata.Empty;
        }

        internal LottieCompositionMetadata LottieMetadata { get; }

        internal IReadOnlyList<PropertyBinding> PropertyBindings
        {
            get
            {
                if (_propertyBindings is null)
                {
                    if (_sourceMetadata.TryGetValue(s_propertyBindingNamesKey, out var propertyBindingNames))
                    {
                        var list = (IReadOnlyList<PropertyBinding>)propertyBindingNames;
                        _propertyBindings = list.OrderBy(pb => pb.BindingName).ToArray();
                    }
                    else
                    {
                        _propertyBindings = Array.Empty<PropertyBinding>();
                    }
                }

                return _propertyBindings;
            }
        }

        internal double ProgressToFrameNumber(double progress)
            => LottieMetadata.Duration.Frames * progress;
    }
}
