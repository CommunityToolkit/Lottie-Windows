// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgce;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
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
            CompositionEffectFactory? cached = null;
            switch (effect.Type)
            {
                case GraphicsEffectType.CompositeEffect:
                    {
                        var compositeNew = (CompositeEffect)effect;

                        foreach (var anyCached in _effectFactoryCache)
                        {
                            if (anyCached.GetEffect() is not CompositeEffect)
                            {
                                continue;
                            }

                            var compositeCached = (CompositeEffect)anyCached.GetEffect();

                            if (compositeCached.Mode != compositeNew.Mode)
                            {
                                continue;
                            }

                            if (compositeCached.Sources.Count != compositeNew.Sources.Count)
                            {
                                continue;
                            }

                            bool ok = true;
                            foreach (var source in compositeNew.Sources)
                            {
                                if (compositeCached.Sources.Where(x => x.Name == source.Name).ToList().Count == 0)
                                {
                                    ok = false;
                                    break;
                                }
                            }

                            if (ok)
                            {
                                cached = anyCached;
                                break;
                            }
                        }

                        break;
                    }

                case GraphicsEffectType.GaussianBlurEffect:
                    {
                        var gaussianNew = (GaussianBlurEffect)effect;

                        foreach (var anyCached in _effectFactoryCache)
                        {
                            if (anyCached.GetEffect() is not GaussianBlurEffect)
                            {
                                continue;
                            }

                            var gaussianCached = (GaussianBlurEffect)anyCached.GetEffect();

                            if (gaussianCached.BlurAmount != gaussianNew.BlurAmount)
                            {
                                continue;
                            }

                            if (gaussianCached.Sources.Count != gaussianNew.Sources.Count)
                            {
                                continue;
                            }

                            bool ok = true;
                            foreach (var source in gaussianNew.Sources)
                            {
                                if (gaussianCached.Sources.Where(x => x.Name == source.Name).ToList().Count == 0)
                                {
                                    ok = false;
                                    break;
                                }
                            }

                            if (ok)
                            {
                                cached = anyCached;
                                break;
                            }
                        }

                        break;
                    }

                default:
                    throw new InvalidOperationException();
            }

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

        public GraphicsEffectBase GetEffect() => _effect;

        /// <inheritdoc/>
        public override CompositionObjectType Type => CompositionObjectType.CompositionEffectFactory;
    }
}
