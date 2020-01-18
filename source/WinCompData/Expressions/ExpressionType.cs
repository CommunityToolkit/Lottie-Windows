// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions
{
    /// <summary>
    /// Describes the type of an <see cref="Expression"/>.
    /// </summary>
#if PUBLIC_WinCompData
    public
#endif
    enum ExpressionType
    {
        Unknown = 0,
        Boolean,
        Color,
        Matrix3x2,
        Matrix4x4,
        Quaternion,
        Scalar,
        Vector2,
        Vector3,
        Vector4,
        Void,
    }
}
