// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace Microsoft.Toolkit.Uwp.UI.Lottie.YamlData
{
    /// <summary>
    /// Base class for factories that create YAML representations of scalars.
    /// </summary>
    /// <remarks>Serializers should subclass this class and add their own methods for the
    /// scalar types they wish to serialize.</remarks>
#if PUBLIC_YamlData
    public
#endif
    abstract class YamlFactory
    {
        protected YamlScalar Scalar(object value, string presentation) => new YamlScalar(value, presentation);
    }
}