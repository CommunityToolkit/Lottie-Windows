// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Mgcg;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinUIXamlMediaData;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;
using Sn = System.Numerics;
using Wg = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wg;
using Wui = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.Tools
{
    /// <summary>
    /// Discovers objects that are shareable and updates a graph so that references to
    /// equivalent shareable objects refer to the same object.
    /// </summary>
    static class Canonicalizer
    {
        // Generates a sequence of ints from 0..int.MaxValue. Used to attach indexes
        // to sequences using Zip.
        static readonly IEnumerable<int> PositiveInts = Enumerable.Range(0, int.MaxValue);

        internal static void Canonicalize<T>(ObjectGraph<T> graph, bool ignoreCommentProperties)
            where T : CanonicalizedNode<T>, new()
        {
            CanonicalizerWorker<T>.Canonicalize(graph, ignoreCommentProperties);
        }

        sealed class CanonicalizerWorker<TNode>
            where TNode : CanonicalizedNode<TNode>, new()
        {
            readonly ObjectGraph<TNode> _graph;
            readonly bool _ignoreCommentProperties;

            CanonicalizerWorker(ObjectGraph<TNode> graph, bool ignoreCommentProperties)
            {
                _graph = graph;
                _ignoreCommentProperties = ignoreCommentProperties;
            }

            internal static void Canonicalize(ObjectGraph<TNode> graph, bool ignoreCommentProperties)
            {
                var canonicalizer = new CanonicalizerWorker<TNode>(graph, ignoreCommentProperties);
                canonicalizer.Canonicalize();
            }

            // Find the nodes that are equivalent and point them all to a single canonical representation.
            void Canonicalize()
            {
                CanonicalizeInsetClips();
                CanonicalizeEllipseGeometries();
                CanonicalizeRectangleGeometries();
                CanonicalizeRoundedRectangleGeometries();

                CanonicalizeCanvasGeometryPaths();

                // CompositionPath must be canonicalized after CanvasGeometry paths.
                CanonicalizeCompositionPaths();

                // CompositionPathGeometry must be canonicalized after CompositionPath.
                CanonicalizeCompositionPathGeometries();

                // Easing functions must be canonicalized before keyframes are canonicalized.
                CanonicalizeLinearEasingFunctions();
                CanonicalizeCubicBezierEasingFunctions();
                CanonicalizeStepEasingFunctions();

                CanonicalizeExpressionAnimations();

                CanonicalizeKeyFrameAnimations<CompositionPath, Expr.Void>(CompositionObjectType.PathKeyFrameAnimation, CompositionPathEqualityComparer);
                CanonicalizeKeyFrameAnimations<bool, Expr.Boolean>(CompositionObjectType.BooleanKeyFrameAnimation);
                CanonicalizeKeyFrameAnimations<Wui.Color, Expr.Color>(CompositionObjectType.ColorKeyFrameAnimation);
                CanonicalizeKeyFrameAnimations<float, Expr.Scalar>(CompositionObjectType.ScalarKeyFrameAnimation);
                CanonicalizeKeyFrameAnimations<Sn.Vector2, Expr.Vector2>(CompositionObjectType.Vector2KeyFrameAnimation);
                CanonicalizeKeyFrameAnimations<Sn.Vector3, Expr.Vector3>(CompositionObjectType.Vector3KeyFrameAnimation);
                CanonicalizeKeyFrameAnimations<Sn.Vector4, Expr.Vector4>(CompositionObjectType.Vector4KeyFrameAnimation);

                // Now that the path animations are canonicalized, canonicalize the CompositionPathGeometries
                // that have animated paths.
                CanonicalizeAnimatedCompositionPathGeometries();

                // ColorKeyFrameAnimations and ExpressionAnimations must be canonicalized before color brushes are canonicalized.
                CanonicalizeColorBrushes();
                CanonicalizeThemeBrushes();

                CanonicalizeLoadedImageSurface(LoadedImageSurface.LoadedImageSurfaceType.FromStream);
                CanonicalizeLoadedImageSurface(LoadedImageSurface.LoadedImageSurfaceType.FromUri);

                // LoadedImageSurfaces must be canonicalized before surface brushes are canonicalized.
                CanonicalizeCompositionSurfaceBrushes();

                // Expression animations and anything that can be referenced by an expression
                // animation on a gradient stop must be canonicalized before gradient stops.
                CanonicalizeColorGradientStops();
            }

            TNode NodeFor(Wg.IGeometrySource2D obj) => _graph[obj].Canonical;

            TNode NodeFor(CompositionObject obj) => _graph[obj].Canonical;

            TNode NodeFor(CompositionPath obj) => _graph[obj].Canonical;

            TNode NodeFor(LoadedImageSurface obj) => _graph[obj].Canonical;

            TNode NodeFor(ICompositionSurface obj)
            {
                switch (obj)
                {
                    case CompositionObject compositionObject:
                        return _graph[compositionObject].Canonical;
                    case LoadedImageSurface loadedImageSurface:
                        return _graph[loadedImageSurface].Canonical;
                    default:
                        throw new InvalidOperationException();
                }
            }

            TC CanonicalObject<TC>(Wg.IGeometrySource2D obj) => (TC)NodeFor(obj).Object;

            TC CanonicalObject<TC>(CompositionObject obj) => (TC)NodeFor(obj).Object;

            TC CanonicalObject<TC>(CompositionPath obj) => (TC)NodeFor(obj).Object;

            TC CanonicalObject<TC>(LoadedImageSurface obj) => (TC)NodeFor(obj).Object;

            TC CanonicalObject<TC>(ICompositionSurface obj) => (TC)NodeFor(obj).Object;

            IEnumerable<(TNode Node, TC Object)> GetCompositionObjects<TC>(CompositionObjectType type)
                where TC : CompositionObject
            {
                return _graph.CompositionObjectNodes.Where(n => n.Object.Type == type).Select(n => (n.Node, (TC)n.Object));
            }

            IEnumerable<(TNode Node, TC Object)> GetCanonicalizableCompositionObjects<TC>(CompositionObjectType type)
                where TC : CompositionObject
            {
                var items = GetCompositionObjects<TC>(type);
                return
                    from item in items
                    let obj = item.Object
                    where (_ignoreCommentProperties || obj.Comment is null)
                       && obj.Properties.Names.Count == 0
                       && obj.Animators.Count == 0
                    select (item.Node, obj);
            }

            IEnumerable<(TNode Node, TC Object)> GetCanonicalizableCanvasGeometries<TC>(CanvasGeometry.GeometryType type)
                where TC : CanvasGeometry
            {
                return
                    from item in _graph.CanvasGeometryNodes
                    let obj = item.Object
                    where obj.Type == type
                    select (item.Node, (TC)obj);
            }

            IEnumerable<(TNode Node, TC Object)> GetCanonicalizableLoadedImageSurfaces<TC>(LoadedImageSurface.LoadedImageSurfaceType type)
                where TC : LoadedImageSurface
            {
                return
                    from item in _graph.LoadedImageSurfaceNodes
                    let obj = item.Object
                    where obj.Type == type
                    select (item.Node, (TC)obj);
            }

            void CanonicalizeExpressionAnimations()
            {
                var items = GetCanonicalizableCompositionObjects<ExpressionAnimation>(CompositionObjectType.ExpressionAnimation);

                // TODO - handle more than one reference parameter.
                var grouping =
                    from item in items
                    where item.Object.ReferenceParameters.Count() == 1
                    group item.Node by GetExpressionAnimationKey1(item.Object)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            (string expression, string, string, CompositionObject) GetExpressionAnimationKey1(ExpressionAnimation animation)
            {
                var rp0 = animation.ReferenceParameters.First();

                return (animation.Expression.ToText(), animation.Target, rp0.Key, CanonicalObject<CompositionObject>(rp0.Value));
            }

            void CanonicalizeKeyFrameAnimations<TKFA, TExpression>(CompositionObjectType animationType)
                where TExpression : Expression_<TExpression>
                => CanonicalizeKeyFrameAnimations<TKFA, TExpression>(animationType, SimpleEqualityComparer<TKFA>);

            void CanonicalizeKeyFrameAnimations<TKFA, TExpression>(
                CompositionObjectType animationType,
                Func<TKFA, TKFA, bool> equalityComparer)
                where TExpression : Expression_<TExpression>
            {
                var items = GetCanonicalizableCompositionObjects<KeyFrameAnimation<TKFA, TExpression>>(animationType);

                var grouping =
                    from item in items
                    group item.Node by new KeyFrameAnimationKey<TKFA, TExpression>(this, item.Object, equalityComparer)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            // Returns true if the a and b are the same CompositionObject after canonicalization.
            bool CompositionPathEqualityComparer(CompositionPath a, CompositionPath b)
                => ReferenceEquals(NodeFor(a), NodeFor(b));

            bool SimpleEqualityComparer<T>(T a, T b)
                => a.Equals(b);

            sealed class KeyFrameAnimationKey<TKFA, TExpression>
                where TExpression : Expression_<TExpression>
            {
                readonly CanonicalizerWorker<TNode> _owner;
                readonly KeyFrameAnimation<TKFA, TExpression> _obj;
                readonly Func<TKFA, TKFA, bool> _equalityComparer;

                internal KeyFrameAnimationKey(
                    CanonicalizerWorker<TNode> owner,
                    KeyFrameAnimation<TKFA, TExpression> obj,
                    Func<TKFA, TKFA, bool> equalityComparer)
                {
                    _owner = owner;
                    _obj = obj;
                    _equalityComparer = equalityComparer;
                }

                public override int GetHashCode()
                {
                    // Not the perfect hash, but not terrible
                    return _obj.KeyFrameCount ^ (int)_obj.Duration.Ticks;
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(this, obj))
                    {
                        return true;
                    }

                    var other = obj as KeyFrameAnimationKey<TKFA, TExpression>;
                    if (other is null)
                    {
                        return false;
                    }

                    var thisObj = _obj;
                    var otherObj = other._obj;

                    if (thisObj.Duration != otherObj.Duration)
                    {
                        return false;
                    }

                    if (thisObj.KeyFrameCount != otherObj.KeyFrameCount)
                    {
                        return false;
                    }

                    if (thisObj.Target != otherObj.Target)
                    {
                        return false;
                    }

                    var thisKfs = thisObj.KeyFrames.ToArray();
                    var otherKfs = otherObj.KeyFrames.ToArray();

                    for (var i = 0; i < thisKfs.Length; i++)
                    {
                        var thisKf = thisKfs[i];
                        var otherKf = otherKfs[i];
                        if (thisKf.Progress != otherKf.Progress)
                        {
                            return false;
                        }

                        if (thisKf.Type != otherKf.Type)
                        {
                            return false;
                        }

                        if (thisKf.Easing != null)
                        {
                            if (otherKf.Easing is null || _owner.NodeFor(thisKf.Easing) != _owner.NodeFor(otherKf.Easing))
                            {
                                return false;
                            }
                        }

                        switch (thisKf.Type)
                        {
                            case KeyFrameType.Expression:
                                var thisExpressionKeyFrame = (KeyFrameAnimation<TKFA, TExpression>.ExpressionKeyFrame)thisKf;
                                var otherExpressionKeyFrame = (KeyFrameAnimation<TKFA, TExpression>.ExpressionKeyFrame)otherKf;
                                if (thisExpressionKeyFrame.Expression != otherExpressionKeyFrame.Expression)
                                {
                                    return false;
                                }

                                break;
                            case KeyFrameType.Value:
                                var thisValueKeyFrame = (KeyFrameAnimation<TKFA, TExpression>.ValueKeyFrame)thisKf;
                                var otherValueKeyFrame = (KeyFrameAnimation<TKFA, TExpression>.ValueKeyFrame)otherKf;
                                if (!_equalityComparer(thisValueKeyFrame.Value, otherValueKeyFrame.Value))
                                {
                                    return false;
                                }

                                break;
                            default:
                                break;
                        }
                    }

                    return true;
                }
            }

            void CanonicalizeColorBrushes()
            {
                // Canonicalize color brushes that have no animations, or have just a Color animation.
                var nodes = GetCompositionObjects<CompositionColorBrush>(CompositionObjectType.CompositionColorBrush);

                var items =
                    from item in nodes
                    let obj = item.Object
                    where (_ignoreCommentProperties || obj.Comment is null)
                       && obj.Properties.Names.Count == 0
                    select (item.Node, obj);

                var grouping =
                    from item in items
                    let obj = item.obj
                    let animators = obj.Animators.ToArray()
                    where animators.Length == 0 || (animators.Length == 1 && animators[0].AnimatedProperty == "Color")
                    let animator = animators.FirstOrDefault()
                    let canonicalAnimator = animator is null ? null : CanonicalObject<CompositionAnimation>(animator.Animation)
                    group item.Node by (obj.Color, canonicalAnimator) into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeThemeBrushes()
            {
                // Canonicalize color brushes that have a single property set value. These
                // are likely to be themed color brushes.
                var nodes = GetCompositionObjects<CompositionColorBrush>(CompositionObjectType.CompositionColorBrush);

                var items =
                    from item in nodes
                    let obj = item.Object
                    where (_ignoreCommentProperties || obj.Comment is null)
                        && obj.Color is null
                        && obj.Properties.Names.Count == 1
                    select (item.Node, obj);

                var grouping =
                    from item in items
                    let obj = item.obj
                    let animators = obj.Animators.ToArray()
                    where animators.Length == 1
                    let animator = animators[0]
                    where animator.AnimatedProperty == "Color"
                        && animator.Animation.Type == CompositionObjectType.ExpressionAnimation
                    let key = new ThemeBrushKey(this, obj)
                    group item.Node by key into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            sealed class ThemeBrushKey : IEquatable<ThemeBrushKey>
            {
                readonly CanonicalizerWorker<TNode> _owner;
                readonly CompositionColorBrush _brush;
                readonly ExpressionAnimation _animation;

                internal ThemeBrushKey(CanonicalizerWorker<TNode> owner, CompositionColorBrush brush)
                {
                    _owner = owner;
                    var animators = brush.Animators.ToArray();

                    if (animators.Length != 1)
                    {
                        throw new InvalidOperationException();
                    }

                    var animator = animators[0];
                    if (animator.AnimatedProperty != "Color")
                    {
                        throw new InvalidOperationException();
                    }

                    if (animator.Animation.Type != CompositionObjectType.ExpressionAnimation)
                    {
                        throw new InvalidOperationException();
                    }

                    _brush = brush;
                    _animation = (ExpressionAnimation)animator.Animation;
                }

                public bool Equals(ThemeBrushKey other)
                {
                    var otherAnimation = other._animation;
                    var thisText = _animation.Expression.ToText();

                    var otherText = otherAnimation.Expression.ToText();
                    if (thisText != otherText)
                    {
                        return false;
                    }

                    // The animations have the same text. Are their reference parameters the same?
                    var thisRefs = _animation.ReferenceParameters.ToArray();
                    var otherRefs = otherAnimation.ReferenceParameters.ToArray();

                    if (thisRefs.Length != otherRefs.Length)
                    {
                        return false;
                    }

                    // Compare the reference parameters. They are always returned in alphabetical order.
                    for (var i = 0; i < thisRefs.Length; i++)
                    {
                        var thisRef = thisRefs[i];
                        var otherRef = otherRefs[i];
                        if (thisRef.Key != otherRef.Key)
                        {
                            // The reference have different names.
                            return false;
                        }

                        var thisRefValue = thisRef.Value;
                        var otherRefValue = otherRef.Value;

                        if (thisRefValue != otherRefValue)
                        {
                            // The values of the references are different, but they might be self
                            // references (i.e. references back to the property set of the brush).
                            // Check that.
                            if (thisRefValue != _brush || otherRefValue != other._brush)
                            {
                                // They're not direct self references. They may be references to
                                // the property set owned by the brush.
                                if (thisRefValue is CompositionPropertySet thisPropertySet &&
                                    otherRefValue is CompositionPropertySet otherPropertySet)
                                {
                                    // They're references to a property set. Is it the property set on the brush?
                                    if (thisPropertySet.Owner != _brush ||
                                        otherPropertySet.Owner != other._brush)
                                    {
                                        return false;
                                    }

                                    // They're both references to their own property set. Make sure each property set
                                    // has the same properties and the same animations.
                                    var thispAnimators = thisPropertySet.Animators;
                                    var otherpAnimators = otherPropertySet.Animators;
                                    if (thispAnimators.Count != otherpAnimators.Count)
                                    {
                                        return false;
                                    }

                                    if (thispAnimators.Count != 1)
                                    {
                                        // For now we only handle a single animator.
                                        return false;
                                    }

                                    var thisAnimator = thispAnimators[0];
                                    var otherAnimator = otherpAnimators[0];
                                    if (thisAnimator.AnimatedProperty != otherAnimator.AnimatedProperty)
                                    {
                                        return false;
                                    }

                                    if (_owner.NodeFor(thisAnimator.Animation).Canonical != _owner.NodeFor(otherAnimator.Animation))
                                    {
                                        return false;
                                    }
                                }
                                else
                                {
                                    // They're not references to a property set.
                                    return false;
                                }
                            }
                        }
                    }

                    return true;
                }

                public override int GetHashCode()
                    => _animation.Expression.ToText().GetHashCode();

                public override bool Equals(object obj) => Equals(obj as ThemeBrushKey);
            }

            void CanonicalizeColorGradientStops()
            {
                // CompositionColorGradientStopCollections do not support having the same
                // CompositionColorGradientStop appearing more than once in the same collection, so
                // we have to special-case canonicalization so that they are shared between
                // collections, but not within a collection.

                // Get the gradient brushes.
                var gradientBrushes = GetCompositionObjects<CompositionGradientBrush>(CompositionObjectType.CompositionLinearGradientBrush).Concat(
                    GetCompositionObjects<CompositionGradientBrush>(CompositionObjectType.CompositionRadialGradientBrush));

                // For each CompositionGradientBrush, get the non-animated stops, and give each an index
                // indicating whether it is the 1st, 2nd, 3rd, etc stop with that color and offset
                // in the collection.
                var nonAnimatedStopsWithIndex =
                    from b in gradientBrushes
                    let nonAnimatedStops = from s in b.Object.ColorStops
                                           where (_ignoreCommentProperties || s.Comment is null)
                                              && s.Properties.Names.Count == 0 && !s.Animators.Any()
                                           group s by (s.Color, s.Offset) into g
                                           from s2 in g.Zip(PositiveInts, (x, y) => (Stop: x, Index: y))
                                           select s2
                    from s in nonAnimatedStops
                    select s;

                // Group by stops that have the same color and index.
                var grouping =
                    from item in nonAnimatedStopsWithIndex
                    let obj = item.Stop
                    group NodeFor(obj) by (obj.Color, obj.Offset, item.Index)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeCompositionSurfaceBrushes()
            {
                // Canonicalize surface brushes that have no animations.
                var nodes = GetCompositionObjects<CompositionSurfaceBrush>(CompositionObjectType.CompositionSurfaceBrush);

                var items =
                    from item in nodes
                    let obj = item.Object
                    where (_ignoreCommentProperties || obj.Comment is null)
                       && obj.Properties.Names.Count == 0
                    select (item.Node, obj);

                var grouping =
                    from item in items
                    let obj = item.obj
                    where obj.Animators.Count == 0
                    group item.Node by CanonicalObject<ICompositionSurface>(obj.Surface) into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeEllipseGeometries()
            {
                var items = GetCanonicalizableCompositionObjects<CompositionEllipseGeometry>(CompositionObjectType.CompositionEllipseGeometry);

                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by (
                        obj.Center,
                        obj.Radius,
                        obj.TrimStart,
                        obj.TrimEnd,
                        obj.TrimOffset)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeRectangleGeometries()
            {
                var items = GetCanonicalizableCompositionObjects<CompositionRectangleGeometry>(CompositionObjectType.CompositionRectangleGeometry);

                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by (
                        obj.Offset,
                        obj.Size,
                        obj.TrimStart,
                        obj.TrimEnd,
                        obj.TrimOffset)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeRoundedRectangleGeometries()
            {
                var items = GetCanonicalizableCompositionObjects<CompositionRoundedRectangleGeometry>(CompositionObjectType.CompositionRoundedRectangleGeometry);

                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by (
                        obj.Offset,
                        obj.Size,
                        obj.CornerRadius,
                        obj.TrimStart,
                        obj.TrimEnd,
                        obj.TrimOffset)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeCompositionPathGeometries()
            {
                var items = GetCanonicalizableCompositionObjects<CompositionPathGeometry>(CompositionObjectType.CompositionPathGeometry);

                var grouping =
                    from item in items
                    let obj = item.Object
                    let path = CanonicalObject<CompositionPath>(obj.Path)
                    group item.Node by (
                        path,
                        obj.TrimStart,
                        obj.TrimEnd,
                        obj.TrimOffset)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeAnimatedCompositionPathGeometries()
            {
                var items =
                    from item in GetCompositionObjects<CompositionPathGeometry>(CompositionObjectType.CompositionPathGeometry)
                    let obj = item.Object
                    where (_ignoreCommentProperties || obj.Comment is null)
                       && obj.Properties.Names.Count == 0
                       && obj.Animators.Count == 1
                    let animator = obj.Animators[0]
                    where animator.AnimatedProperty == "Path"
                    select (Node:item.Node, Object:obj);

                var grouping =
                    from item in items
                    let obj = item.Object
                    let animation = CanonicalObject<PathKeyFrameAnimation>(obj.Animators[0].Animation)
                    group item.Node by (
                        animation,
                        obj.TrimStart,
                        obj.TrimEnd,
                        obj.TrimOffset)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeCanvasGeometryPaths()
            {
                var items = GetCanonicalizableCanvasGeometries<CanvasGeometry.Path>(CanvasGeometry.GeometryType.Path);
                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by obj into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeCompositionPaths()
            {
                var grouping =
                    from item in _graph.CompositionPathNodes
                    let obj = item.Object
                    let canonicalSource = CanonicalObject<CanvasGeometry>(obj.Source)
                    group item.Node by canonicalSource into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeInsetClips()
            {
                var items = GetCanonicalizableCompositionObjects<InsetClip>(CompositionObjectType.InsetClip);

                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by (
                        obj.BottomInset,
                        obj.LeftInset,
                        obj.RightInset,
                        obj.TopInset,
                        obj.CenterPoint,
                        obj.Scale)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeCubicBezierEasingFunctions()
            {
                var items = GetCanonicalizableCompositionObjects<CubicBezierEasingFunction>(CompositionObjectType.CubicBezierEasingFunction);

                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by (obj.ControlPoint1, obj.ControlPoint2)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeLinearEasingFunctions()
            {
                var items = GetCanonicalizableCompositionObjects<LinearEasingFunction>(CompositionObjectType.LinearEasingFunction);

                // Every LinearEasingFunction is equivalent.
                var grouping =
                    from item in items
                    group item.Node by true into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeStepEasingFunctions()
            {
                var items = GetCanonicalizableCompositionObjects<StepEasingFunction>(CompositionObjectType.StepEasingFunction);

                var grouping =
                    from item in items
                    let obj = item.Object
                    group item.Node by (
                        obj.FinalStep,
                        obj.InitialStep,
                        obj.IsFinalStepSingleFrame,
                        obj.IsInitialStepSingleFrame,
                        obj.StepCount)
                    into grouped
                    select grouped;

                CanonicalizeGrouping(grouping);
            }

            void CanonicalizeLoadedImageSurface(LoadedImageSurface.LoadedImageSurfaceType type)
            {
                // Canonicalize LoadedImageSurfaces.
                var items = GetCanonicalizableLoadedImageSurfaces<LoadedImageSurface>(type);

                switch (type)
                {
                    case LoadedImageSurface.LoadedImageSurfaceType.FromStream:
                        var grouping = items.GroupBy(i => ((LoadedImageSurfaceFromStream)i.Object).Bytes, i => i.Node, ByteArrayComparer.Instance);
                        CanonicalizeGrouping(grouping);
                        break;
                    case LoadedImageSurface.LoadedImageSurfaceType.FromUri:
                        var groupingExternal =
                            from item in items
                            let obj = (LoadedImageSurfaceFromUri)item.Object
                            group item.Node by obj.Uri into grouped
                            select grouped;
                        CanonicalizeGrouping(groupingExternal);
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            static void CanonicalizeGrouping<TKey>(IEnumerable<IGrouping<TKey, TNode>> grouping)
            {
                foreach (var group in grouping)
                {
                    // Pick a node to be the canonical node. Any node in the group is suitable
                    // because, by definition, they are all equivalent, but for consistency
                    // we always pick the node that appears first in the traversal of the tree.
                    var nodes = group.ToArray();
                    if (nodes.Length > 1)
                    {
                        var canonical = nodes.OrderBy(n => n.Position).FirstOrDefault();

                        // Point every node to the designated canonical node.
                        foreach (var node in nodes)
                        {
                            node.Canonical = canonical;
                        }
                    }
                }
            }

            sealed class ByteArrayComparer : IEqualityComparer<byte[]>
            {
                ByteArrayComparer()
                {
                }

                internal static ByteArrayComparer Instance { get; } = new ByteArrayComparer();

                bool IEqualityComparer<byte[]>.Equals(byte[] x, byte[] y)
                {
                    return x.SequenceEqual(y);
                }

                int IEqualityComparer<byte[]>.GetHashCode(byte[] obj)
                {
                    // This is not a great hash code but is good enough for what we need.
                    return obj.Length;
                }
            }
        }
    }
}
