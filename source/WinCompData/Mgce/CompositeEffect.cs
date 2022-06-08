// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgc;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Mgce
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositeEffect : GraphicsEffectBase
    {
        public CompositeEffect(CanvasComposite mode, IList<CompositionEffectSourceParameter> sources)
        {
            Mode = mode;
            _sources = sources.ToList();
        }

        public CanvasComposite Mode { get; }

        private List<CompositionEffectSourceParameter> _sources;

        public override IReadOnlyList<CompositionEffectSourceParameter> Sources => _sources.AsReadOnly();

        public override GraphicsEffectType Type => GraphicsEffectType.CompositeEffect;

        public override bool Equals(object? obj)
        {
            if (!(obj is CompositeEffect))
            {
                return false;
            }

            var other = (CompositeEffect)obj;

            if (other.Mode != Mode)
            {
                return false;
            }

            if (other.Sources.Count != Sources.Count)
            {
                return false;
            }

            foreach (var source in Sources)
            {
                if (!other.Sources.Contains(source))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hashCode = -1419366798;
            hashCode = (hashCode * -1521134295) + Mode.GetHashCode();
            hashCode = (hashCode * -1521134295) + _sources.Sum(x => x.GetHashCode());
            return hashCode;
        }
    }
}
