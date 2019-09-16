// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData
{
    [MetaData.UapVersion(2)]
#if PUBLIC_WinCompData
    public
#endif
    abstract class CompositionAnimation : CompositionObject
    {
        readonly Dictionary<string, CompositionObject> _referencedParameters = new Dictionary<string, CompositionObject>();

        private protected CompositionAnimation(CompositionAnimation other)
        {
            if (other != null)
            {
                foreach (var pair in other._referencedParameters)
                {
                    _referencedParameters.Add(pair.Key, pair.Value);
                }

                Target = other.Target;

                LongDescription = other.LongDescription;
                ShortDescription = other.ShortDescription;
                Comment = other.Comment;
            }
        }

        public string Target { get; set; }

        // True iff this object's state is expected to never change.
        public bool IsFrozen { get; private set; }

        /// <summary>
        /// Marks the <see cref="CompositionAnimation"/> to indicate that its state
        /// should never change again. Note that this is a weak guarantee as there
        /// are not checks on all mutators to ensure that changes aren't made after
        /// freezing. However correct code must never mutate a frozen object.
        /// </summary>
        public void Freeze()
        {
            IsFrozen = true;
        }

        public void SetReferenceParameter(string key, CompositionObject compositionObject)
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException();
            }

            _referencedParameters.Add(key, compositionObject);
        }

        public IEnumerable<KeyValuePair<string, CompositionObject>> ReferenceParameters => _referencedParameters;

        internal abstract CompositionAnimation Clone();
    }
}
