// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;

using CommunityToolkit.WinUI.Lottie.WinCompData.Mgce;

namespace CommunityToolkit.WinUI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionEffectFactory : CompositionObject
    {
        readonly GraphicsEffectBase _effect;

        internal CompositionEffectFactory(GraphicsEffectBase effect)
        {
            _effect = effect;
        }

        private static IList<CompositionEffectFactory> _effectFactoryCache = new List<CompositionEffectFactory>();

        public static CompositionEffectFactory GetFactoryCached(GraphicsEffectBase effect)
        {
            var found = _effectFactoryCache.Where(f => f.Effect.Equals(effect)).ToList();
            var cached = found.Count == 0 ? null : found.First();

            if (cached is null)
            {
                cached = new CompositionEffectFactory(effect);
                _effectFactoryCache.Add(cached);
            }

            return cached!;
        }

        public CompositionEffectBrush CreateBrush()
        {
            return new CompositionEffectBrush(this);
        }

        public GraphicsEffectBase Effect => _effect;

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectFactory;
    }
}
