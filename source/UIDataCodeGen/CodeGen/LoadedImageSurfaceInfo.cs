// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Wmd = CommunityToolkit.WinUI.Lottie.WinUIXamlMediaData;

namespace CommunityToolkit.WinUI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Describes LoadedImageSurface objects in the composition.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class LoadedImageSurfaceInfo
    {
        internal LoadedImageSurfaceInfo(
            string typeName,
            string name,
            string? comment,
            string fieldName,
            string bytesFieldName,
            Uri imageUri,
            Wmd.LoadedImageSurface.LoadedImageSurfaceType loadedImageSurfaceType,
            IReadOnlyList<byte>? bytes)
        {
            TypeName = typeName;
            Name = name;
            Comment = comment;
            FieldName = fieldName;
            BytesFieldName = bytesFieldName;
            ImageUri = imageUri;
            LoadedImageSurfaceType = loadedImageSurfaceType;
            Bytes = bytes;
        }

        public string TypeName { get; }

        public string Name { get; }

        public string? Comment { get; }

        public string FieldName { get; }

        public string BytesFieldName { get; }

        public Uri ImageUri { get; }

        public Wmd.LoadedImageSurface.LoadedImageSurfaceType LoadedImageSurfaceType { get; }

        public IReadOnlyList<byte>? Bytes { get; }
    }
}
