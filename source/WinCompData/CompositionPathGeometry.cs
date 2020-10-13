// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(6)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionPathGeometry : CompositionGeometry
    {
        internal CompositionPathGeometry()
        {
        }

        internal CompositionPathGeometry(CompositionPath? path)
        {
            Path = path;
        }

        public CompositionPath? Path { get; set; }

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionPathGeometry;
    }
}
