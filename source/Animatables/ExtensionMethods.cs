// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.Animatables
{
#if PUBLIC_Animatables
    public
#endif
    static class ExtensionMethods
    {
        public static IAnimatableVector2 WithoutZ(this IAnimatableVector3 animatableVector3)
        {
            switch (animatableVector3.Type)
            {
                case AnimatableVector3Type.Vector3:
                    {
                        var v3 = (AnimatableVector3)animatableVector3;
                        return v3.IsAnimated
                            ? new AnimatableVector2(v3.KeyFrames.Select(
                                kf => new KeyFrame<Vector2>(kf.Frame, new Vector2(kf.Value.X, kf.Value.Y), kf.Easing)))
                            : new AnimatableVector2(new Vector2(v3.InitialValue.X, v3.InitialValue.Y));
                    }

                case AnimatableVector3Type.XYZ:
                    {
                        var vxyz = (AnimatableXYZ)animatableVector3;
                        return new AnimatableXY(vxyz.X, vxyz.Y);
                    }

                default:
                    throw Exceptions.Unreachable;
            }
        }
    }
}
