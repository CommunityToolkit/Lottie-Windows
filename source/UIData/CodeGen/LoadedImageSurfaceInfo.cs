// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using Wmd = Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Describes LoadedImageSurface objects in the composition.
    /// </summary>
#if PUBLIC_UIData
    public
#endif
    sealed class LoadedImageSurfaceInfo
    {
        internal LoadedImageSurfaceInfo(
    string typeName,
    string name,
    string fieldName,
    string bytesFieldName,
    Uri imageUri,
    Wmd.LoadedImageSurface.LoadedImageSurfaceType loadedImageSurfaceType,
    byte[] bytes)
        {
            TypeName = typeName;
            Name = name;
            FieldName = fieldName;
            BytesFieldName = bytesFieldName;
            ImageUri = imageUri;
            LoadedImageSurfaceType = loadedImageSurfaceType;
            Bytes = bytes;
        }

        public string TypeName { get; }

        public string Name { get; }

        public string FieldName { get; }

        public string BytesFieldName { get; }

        public Uri ImageUri { get; }

        public Wmd.LoadedImageSurface.LoadedImageSurfaceType LoadedImageSurfaceType { get; }

        public byte[] Bytes { get; }
    }
}
