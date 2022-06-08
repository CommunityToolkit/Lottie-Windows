// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class GaussianBlurEffect : GraphicsEffectBase
    {
        public GaussianBlurEffect(float blurAmount, CompositionEffectSourceParameter source)
        {
            BlurAmount = blurAmount;
            _source = source;
        }

        public float BlurAmount { get; }

        private CompositionEffectSourceParameter _source;

        public override IReadOnlyList<CompositionEffectSourceParameter> Sources => new List<CompositionEffectSourceParameter>() { _source };

        public override GraphicsEffectType Type => GraphicsEffectType.GaussianBlurEffect;

        public override bool Equals(object obj)
        {
            if (!(obj is GaussianBlurEffect))
            {
                return false;
            }

            var other = (GaussianBlurEffect)obj;

            if (other.BlurAmount != BlurAmount)
            {
                return false;
            }

            if (other.Sources.Count != Sources.Count)
            {
                return false;
            }

            return _source?.Equals(((GaussianBlurEffect)obj)._source) ?? false;
        }

        public override int GetHashCode()
        {
            int hashCode = 593574215;
            hashCode = (hashCode * -1521134295) + BlurAmount.GetHashCode();
            hashCode = (hashCode * -1521134295) + _source?.GetHashCode() ?? 0;
            return hashCode;
        }
    }
}
