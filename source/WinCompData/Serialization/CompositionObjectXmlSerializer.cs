// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define RenderCommentsAsXmlComments
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Expr = Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Expressions;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools
{
    /// <summary>
    /// Serializes a <see cref="CompositionObject"/> graph into an XML format.
    /// </summary>
    /// <remarks>The format is only designed for human consumption, and should
    /// not be relied upon for deserialization.</remarks>
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionObjectXmlSerializer
    {
        CompositionObjectXmlSerializer()
        {
        }

        public static XDocument ToXml(CompositionObject compositionObject)
        {
            return new CompositionObjectXmlSerializer().ToXDocument(compositionObject);
        }

        XDocument ToXDocument(CompositionObject compositionObject)
        {
            return new XDocument(FromCompositionObject(compositionObject));
        }

        IEnumerable<XObject> FromCompositionObject(CompositionObject obj)
        {
            if (obj is null)
            {
                yield break;
            }

#if RenderCommentsAsXmlComments
            if (!string.IsNullOrWhiteSpace(obj.Comment))
            {
                yield return new XComment(obj.Comment);
            }
#endif
            switch (obj.Type)
            {
                case CompositionObjectType.AnimationController:
                    yield return FromAnimationController((AnimationController)obj);
                    break;
                case CompositionObjectType.BooleanKeyFrameAnimation:
                    yield return FromBooleanKeyFrameAnimation((BooleanKeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.ColorKeyFrameAnimation:
                    yield return FromColorKeyFrameAnimation((ColorKeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.CompositionColorBrush:
                    yield return FromCompositionColorBrush((CompositionColorBrush)obj);
                    break;
                case CompositionObjectType.CompositionContainerShape:
                    yield return FromCompositionContainerShape((CompositionContainerShape)obj);
                    break;
                case CompositionObjectType.CompositionEllipseGeometry:
                    yield return FromCompositionEllipseGeometry((CompositionEllipseGeometry)obj);
                    break;
                case CompositionObjectType.CompositionEffectBrush:
                    yield return FromCompositionEffectBrush((CompositionEffectBrush)obj);
                    break;
                case CompositionObjectType.CompositionLinearGradientBrush:
                    yield return FromCompositionLinearGradientBrush((CompositionLinearGradientBrush)obj);
                    break;
                case CompositionObjectType.CompositionPathGeometry:
                    yield return FromCompositionPathGeometry((CompositionPathGeometry)obj);
                    break;
                case CompositionObjectType.CompositionPropertySet:
                    yield return FromCompositionPropertySet((CompositionPropertySet)obj);
                    break;
                case CompositionObjectType.CompositionRadialGradientBrush:
                    yield return FromCompositionRadialGradientBrush((CompositionRadialGradientBrush)obj);
                    break;
                case CompositionObjectType.CompositionRectangleGeometry:
                    yield return FromCompositionRectangleGeometry((CompositionRectangleGeometry)obj);
                    break;
                case CompositionObjectType.CompositionRoundedRectangleGeometry:
                    yield return FromCompositionRoundedRectangleGeometry((CompositionRoundedRectangleGeometry)obj);
                    break;
                case CompositionObjectType.CompositionSpriteShape:
                    yield return FromCompositionSpriteShape((CompositionSpriteShape)obj);
                    break;
                case CompositionObjectType.CompositionViewBox:
                    yield return FromCompositionViewBox((CompositionViewBox)obj);
                    break;
                case CompositionObjectType.ContainerVisual:
                    yield return FromContainerVisual((ContainerVisual)obj);
                    break;
                case CompositionObjectType.CubicBezierEasingFunction:
                    yield return FromCubicBezierEasingFunction((CubicBezierEasingFunction)obj);
                    break;
                case CompositionObjectType.ExpressionAnimation:
                    yield return FromExpressionAnimation((ExpressionAnimation)obj);
                    break;
                case CompositionObjectType.InsetClip:
                    yield return FromInsetClip((InsetClip)obj);
                    break;
                case CompositionObjectType.CompositionGeometricClip:
                    yield return FromCompositionGeometricClip((CompositionGeometricClip)obj);
                    break;
                case CompositionObjectType.LinearEasingFunction:
                    yield return FromLinearEasingFunction((LinearEasingFunction)obj);
                    break;
                case CompositionObjectType.PathKeyFrameAnimation:
                    yield return FromPathKeyFrameAnimation((PathKeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.ScalarKeyFrameAnimation:
                    yield return FromScalarKeyFrameAnimation((ScalarKeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.ShapeVisual:
                    yield return FromShapeVisual((ShapeVisual)obj);
                    break;
                case CompositionObjectType.StepEasingFunction:
                    yield return FromStepEasingFunction((StepEasingFunction)obj);
                    break;
                case CompositionObjectType.Vector2KeyFrameAnimation:
                    yield return FromVector2KeyFrameAnimation((Vector2KeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.Vector3KeyFrameAnimation:
                    yield return FromVector3KeyFrameAnimation((Vector3KeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.Vector4KeyFrameAnimation:
                    yield return FromVector4KeyFrameAnimation((Vector4KeyFrameAnimation)obj);
                    break;
                case CompositionObjectType.SpriteVisual:
                    yield return FromSpriteVisual((SpriteVisual)obj);
                    break;
                case CompositionObjectType.CompositionSurfaceBrush:
                    yield return FromCompositionSurfaceBrush((CompositionSurfaceBrush)obj);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        XElement FromBooleanKeyFrameAnimation(BooleanKeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromColorKeyFrameAnimation(ColorKeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromCompositionColorBrush(CompositionColorBrush obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }

                if (obj.Color.HasValue)
                {
                    var color = obj.Color.Value;
                    yield return new XAttribute("Color", $"#{ToHex(color.A)}{ToHex(color.R)}{ToHex(color.G)}{ToHex(color.B)}");
                }

                string ToHex(byte value) => value.ToString("X2");
            }
        }

        XElement FromCompositionContainerShape(CompositionContainerShape obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionShapeContents(obj))
                {
                    yield return item;
                }

                foreach (var item in obj.Shapes.SelectMany(FromCompositionObject))
                {
                    yield return item;
                }
            }
        }

        XElement FromCompositionEllipseGeometry(CompositionEllipseGeometry obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionGeometryContents(obj))
                {
                    yield return item;
                }

                yield return FromVector2(nameof(obj.Center), obj.Center);
                yield return FromVector2(nameof(obj.Radius), obj.Radius);
            }
        }

        XElement FromCompositionEffectBrush(CompositionEffectBrush obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromCompositionLinearGradientBrush(CompositionLinearGradientBrush obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromCompositionPropertySet(CompositionPropertySet obj)
        {
            return new XElement("CompositionPropertySet", GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromCompositionPath(CompositionPath obj)
        {
            return new XElement("CompositionPath", GetContents());
            IEnumerable<XObject> GetContents()
            {
                yield return FromIGeometrySource2D(obj.Source);
            }
        }

        XElement FromCompositionPathGeometry(CompositionPathGeometry obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionGeometryContents(obj))
                {
                    yield return item;
                }

                if (obj.Path != null)
                {
                    yield return FromCompositionPath(obj.Path);
                }
            }
        }

        XElement FromCompositionRadialGradientBrush(CompositionRadialGradientBrush obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromCompositionRectangleGeometry(CompositionRectangleGeometry obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionGeometryContents(obj))
                {
                    yield return item;
                }

                if (obj.Offset != null)
                {
                    yield return FromVector2(nameof(obj.Offset), obj.Offset.Value);
                }

                yield return FromVector2(nameof(obj.Size), obj.Size);
            }
        }

        XElement FromCompositionRoundedRectangleGeometry(CompositionRoundedRectangleGeometry obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionGeometryContents(obj))
                {
                    yield return item;
                }

                if (obj.Offset != null)
                {
                    yield return FromVector2(nameof(obj.Offset), obj.Offset.Value);
                }

                yield return FromVector2(nameof(obj.Size), obj.Size);
                yield return FromVector2(nameof(obj.CornerRadius), obj.CornerRadius);
            }
        }

        XElement FromCompositionSpriteShape(CompositionSpriteShape obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionShapeContents(obj))
                {
                    yield return item;
                }

                yield return FromVector2DefaultZero(nameof(obj.CenterPoint), obj.CenterPoint);

                if (obj.FillBrush != null)
                {
                    yield return new XElement(nameof(obj.FillBrush), FromCompositionObject(obj.FillBrush));
                }

                if (obj.Geometry != null)
                {
                    yield return new XElement(nameof(obj.Geometry), FromCompositionObject(obj.Geometry));
                }

                if (obj.StrokeBrush != null)
                {
                    yield return new XElement(nameof(obj.StrokeBrush), FromCompositionObject(obj.StrokeBrush));
                }
            }
        }

        XElement FromCompositionViewBox(CompositionViewBox obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }

                yield return FromVector2(nameof(obj.Size), obj.Size);
            }
        }

        XElement FromCubicBezierEasingFunction(CubicBezierEasingFunction obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromInsetClip(InsetClip obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionClipContents(obj))
                {
                    yield return item;
                }

                yield return new XAttribute("LeftInset", obj.LeftInset);
                yield return new XAttribute("TopInset", obj.TopInset);
                yield return new XAttribute("RightInset", obj.RightInset);
                yield return new XAttribute("BottomInset", obj.BottomInset);
            }
        }

        XElement FromCompositionGeometricClip(CompositionGeometricClip obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                if (obj.Geometry != null)
                {
                    yield return new XElement("Geometry", FromCompositionObject(obj.Geometry));
                }
            }
        }

        IEnumerable<XObject> GetCompositionClipContents(CompositionClip obj)
        {
            foreach (var item in GetCompositionObjectContents(obj))
            {
                yield return item;
            }

            yield return FromVector2DefaultZero(nameof(obj.CenterPoint), obj.CenterPoint);
            yield return FromVector2DefaultOne(nameof(obj.Scale), obj.Scale);
        }

        XElement FromLinearEasingFunction(LinearEasingFunction obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromPathKeyFrameAnimation(PathKeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromScalarKeyFrameAnimation(ScalarKeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromContainerVisual(ContainerVisual obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContainerVisualContents(obj));
        }

        XElement FromShapeVisual(ShapeVisual obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetContainerVisualContents(obj))
                {
                    yield return item;
                }

                foreach (var item in FromCompositionObject(obj.ViewBox))
                {
                    yield return item;
                }

                foreach (var item in obj.Shapes.SelectMany(FromCompositionObject))
                {
                    yield return item;
                }
            }
        }

        XElement FromStepEasingFunction(StepEasingFunction obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromVector2KeyFrameAnimation(Vector2KeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromVector3KeyFrameAnimation(Vector3KeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromVector4KeyFrameAnimation(Vector4KeyFrameAnimation obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        XElement FromAnimationController(AnimationController obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }
            }
        }

        IEnumerable<XObject> GetCompositionObjectContents(CompositionObject obj)
        {
#if !RenderCommentsAsXmlComments
            if (!string.IsNullOrWhiteSpace(obj.Comment))
            {
                    yield return new XAttribute("Comment", obj.Comment);
            }
#endif // !RenderCommentsAsXmlComments

            // Find the animations that are targetting properties in the property set.
            var propertySetAnimators =
                from pn in obj.Properties.Names
                from an in obj.Animators
                where an.AnimatedProperty == pn.Key
                select an;

            if (obj.Properties.Names.Count > 0)
            {
                yield return FromCompositionPropertySet(obj.Properties, propertySetAnimators);
            }
        }

        IEnumerable<XObject> GetCompositionGeometryContents(CompositionGeometry obj)
        {
            foreach (var item in GetCompositionObjectContents(obj))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableScalar(nameof(obj.TrimStart), obj.Animators, obj.TrimStart))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableScalar(nameof(obj.TrimEnd), obj.Animators, obj.TrimEnd))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableScalar(nameof(obj.TrimOffset), obj.Animators, obj.TrimOffset))
            {
                yield return item;
            }
        }

        IEnumerable<XObject> GetVisualContents(Visual obj)
        {
            foreach (var item in GetCompositionObjectContents(obj))
            {
                yield return item;
            }

            yield return FromVector2DefaultZero(nameof(obj.Size), obj.Size);

            if (obj.BorderMode.HasValue)
            {
                yield return new XAttribute(nameof(obj.BorderMode), obj.BorderMode.Value);
            }

            foreach (var item in FromAnimatableVector3("Offset", obj.Animators, obj.Offset))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableVector3("CenterPoint", obj.Animators, obj.CenterPoint))
            {
                yield return item;
            }

            if (obj.RotationAngleInDegrees.HasValue)
            {
                yield return new XAttribute("RotationAngleInDegrees", obj.RotationAngleInDegrees.Value);
            }

            foreach (var item in FromAnimatableVector3("Scale", obj.Animators, obj.Scale))
            {
                yield return item;
            }

            if (obj.Clip != null)
            {
                yield return new XElement("Clip", FromCompositionObject(obj.Clip));
            }
        }

        IEnumerable<XObject> GetContainerVisualContents(ContainerVisual obj)
        {
            foreach (var item in GetVisualContents(obj))
            {
                yield return item;
            }

            foreach (var item in obj.Children.SelectMany(FromCompositionObject))
            {
                yield return item;
            }
        }

        IEnumerable<XObject> GetCompositionShapeContents(CompositionShape obj)
        {
            foreach (var item in GetCompositionObjectContents(obj))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableVector2(nameof(obj.CenterPoint), obj.Animators, obj.CenterPoint))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableVector2(nameof(obj.Offset), obj.Animators, obj.Offset))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableScalar(nameof(obj.RotationAngleInDegrees), obj.Animators, obj.RotationAngleInDegrees))
            {
                yield return item;
            }

            foreach (var item in FromAnimatableVector2(nameof(obj.Scale), obj.Animators, obj.Scale))
            {
                yield return item;
            }
        }

        XElement FromIGeometrySource2D(Wg.IGeometrySource2D obj)
        {
            if (obj is Mgcg.CanvasGeometry)
            {
                return new XElement("CanvasGeometry");
            }
            else
            {
                // No other types are currently supported.
                throw new InvalidOperationException();
            }
        }

        XElement FromCompositionPropertySet(CompositionPropertySet obj, IEnumerable<CompositionObject.Animator> animators = null)
        {
            return new XElement("PropertySet", GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var (name, type) in obj.Names)
                {
                    switch (type)
                    {
                        case MetaData.PropertySetValueType.Color:
                            {
                                obj.TryGetColor(name, out var value);
                                foreach (var item in FromAnimatableColor(name, animators, value))
                                {
                                    yield return item;
                                }

                                break;
                            }

                        case MetaData.PropertySetValueType.Scalar:
                            {
                                obj.TryGetScalar(name, out var value);
                                foreach (var item in FromAnimatableScalar(name, animators, value))
                                {
                                    yield return item;
                                }

                                break;
                            }

                        case MetaData.PropertySetValueType.Vector2:
                            {
                                obj.TryGetVector2(name, out var value);
                                foreach (var item in FromAnimatableVector2(name, animators, value))
                                {
                                    yield return item;
                                }

                                break;
                            }

                        case MetaData.PropertySetValueType.Vector3:
                            {
                                obj.TryGetVector3(name, out var value);
                                foreach (var item in FromAnimatableVector3(name, animators, value))
                                {
                                    yield return item;
                                }

                                break;
                            }

                        case MetaData.PropertySetValueType.Vector4:
                            {
                                obj.TryGetVector4(name, out var value);
                                foreach (var item in FromAnimatableVector4(name, animators, value))
                                {
                                    yield return item;
                                }

                                break;
                            }

                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
        }

        string GetCompositionObjectName(CompositionObject obj)
        {
            var name = obj.Type.ToString();
            if (name.StartsWith("AnimatedVisualPlayer"))
            {
                name = name.Substring("AnimatedVisualPlayer".Length);
            }

            return name;
        }

        XElement FromAnimation<T, TExpression>(string name, CompositionAnimation animation, T? initialValue)
            where T : struct
            where TExpression : Expr.Expression_<TExpression>
        {
            switch (animation.Type)
            {
                case CompositionObjectType.ExpressionAnimation:
                    return FromExpressionAnimation((ExpressionAnimation)animation, name);
                case CompositionObjectType.BooleanKeyFrameAnimation:
                case CompositionObjectType.ColorKeyFrameAnimation:
                case CompositionObjectType.PathKeyFrameAnimation:
                case CompositionObjectType.ScalarKeyFrameAnimation:
                case CompositionObjectType.Vector2KeyFrameAnimation:
                case CompositionObjectType.Vector3KeyFrameAnimation:
                case CompositionObjectType.Vector4KeyFrameAnimation:
                    return FromKeyFrameAnimation(name, (KeyFrameAnimation<T, TExpression>)animation, initialValue);
                default:
                    throw new InvalidOperationException();
            }
        }

        XElement FromExpressionAnimation(ExpressionAnimation obj, string name = null)
        {
            return new XElement(name ?? GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetCompositionObjectContents(obj))
                {
                    yield return item;
                }

                if (obj.Target != null && obj.Target != name)
                {
                    yield return new XAttribute("Target", obj.Target);
                }

                yield return new XText(obj.Expression.ToText());
            }
        }

        XElement FromKeyFrameAnimation<T, TExpression>(string name, KeyFrameAnimation<T, TExpression> obj, T? initialValue)
            where T : struct
            where TExpression : Expr.Expression_<TExpression>
        {
            return new XElement(name, GetContents());
            IEnumerable<XObject> GetContents()
            {
                if (obj.Target != null && obj.Target != name)
                {
                    yield return new XAttribute("Target", obj.Target);
                }

                var keyFramesString = string.Join(", ", obj.KeyFrames.Select(kf => $"({GetKeyFrameValue(kf)}@{kf.Progress})"));

                if (initialValue.HasValue)
                {
                    yield return new XText($"{initialValue}, {keyFramesString}");
                }
                else
                {
                    yield return new XText(keyFramesString);
                }
            }
        }

        static string GetKeyFrameValue<T, TExpression>(KeyFrameAnimation<T, TExpression>.KeyFrame kf)
            where TExpression : Expr.Expression_<TExpression>
        {
            switch (kf.Type)
            {
                case KeyFrameType.Expression:
                    var expressionKeyFrame = (KeyFrameAnimation<T, TExpression>.ExpressionKeyFrame)kf;
                    return $"\"{expressionKeyFrame.Expression.ToText()}\"";
                case KeyFrameType.Value:
                    var valueKeyFrame = (KeyFrameAnimation<T, TExpression>.ValueKeyFrame)kf;
                    return valueKeyFrame.Value.ToString();
                default:
                    throw new InvalidOperationException();
            }
        }

        IEnumerable<XObject> FromAnimatableColor(string name, IEnumerable<CompositionObject.Animator> animators, Color? initialValue)
        {
            var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

            if (animation != null)
            {
                yield return FromAnimation<Color, Expr.Color>(name, animation, initialValue);
            }
            else
            {
                if (initialValue.HasValue)
                {
                    yield return FromColor(name, initialValue.Value);
                }
            }
        }

        IEnumerable<XObject> FromAnimatableScalar(string name, IEnumerable<CompositionObject.Animator> animators, float? initialValue)
        {
            var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

            if (animation != null)
            {
                yield return FromAnimation<float, Expr.Scalar>(name, animation, initialValue);
            }
            else
            {
                if (initialValue.HasValue)
                {
                    yield return FromScalar(name, initialValue.Value);
                }
            }
        }

        IEnumerable<XObject> FromAnimatableVector2(string name, IEnumerable<CompositionObject.Animator> animators, Vector2? initialValue)
        {
            var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

            if (animation != null)
            {
                yield return FromAnimation<Vector2, Expr.Vector2>(name, animation, initialValue);
            }
            else
            {
                if (initialValue.HasValue)
                {
                    yield return FromVector2(name, initialValue.Value);
                }
            }
        }

        IEnumerable<XObject> FromAnimatableVector3(string name, IEnumerable<CompositionObject.Animator> animators, Vector3? initialValue)
        {
            var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

            if (animation != null)
            {
                yield return FromAnimation<Vector3, Expr.Vector3>(name, animation, initialValue);
            }
            else
            {
                if (initialValue.HasValue)
                {
                    yield return FromVector3(name, initialValue.Value);
                }
            }
        }

        IEnumerable<XObject> FromAnimatableVector4(string name, IEnumerable<CompositionObject.Animator> animators, Vector4? initialValue)
        {
            var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

            if (animation != null)
            {
                yield return FromAnimation<Vector4, Expr.Vector4>(name, animation, initialValue);
            }
            else
            {
                if (initialValue.HasValue)
                {
                    yield return FromVector4(name, initialValue.Value);
                }
            }
        }

        XElement FromColor(string name, Color value)
        {
            return new XElement(name, new XAttribute("ColorValue", value));
        }

        XElement FromScalar(string name, float value)
        {
            return new XElement(name, new XAttribute("ScalarValue", value));
        }

        XElement FromVector2DefaultZero(string name, Vector2? obj) => FromVector2(name, obj, new Vector2(0, 0));

        XElement FromVector2DefaultOne(string name, Vector2? obj) => FromVector2(name, obj, new Vector2(1, 1));

        XElement FromVector2(string name, Vector2? obj, Vector2 defaultIfNull)
            => FromVector2(name, obj.HasValue ? obj.Value : defaultIfNull);

        XElement FromVector2(string name, Vector2 obj)
            => new XElement(
                name,
                new XAttribute(nameof(obj.X), obj.X),
                new XAttribute(nameof(obj.Y), obj.Y));

        XElement FromVector3(string name, Vector3 obj)
            => new XElement(
                name,
                new XAttribute(nameof(obj.X), obj.X),
                new XAttribute(nameof(obj.Y), obj.Y),
                new XAttribute(nameof(obj.Z), obj.Z));

        XElement FromVector4(string name, Vector4 obj)
            => new XElement(
                name,
                new XAttribute(nameof(obj.X), obj.X),
                new XAttribute(nameof(obj.Y), obj.Y),
                new XAttribute(nameof(obj.Z), obj.Z),
                new XAttribute(nameof(obj.W), obj.W));

        XElement FromSpriteVisual(SpriteVisual obj)
        {
            return new XElement(GetCompositionObjectName(obj), GetContents());
            IEnumerable<XObject> GetContents()
            {
                foreach (var item in GetVisualContents(obj))
                {
                    yield return item;
                }

                if (obj.Brush != null)
                {
                    yield return new XElement(nameof(obj.Brush), FromCompositionObject(obj.Brush));
                }
            }
        }

        XElement FromCompositionSurfaceBrush(CompositionSurfaceBrush obj)
        {
            return new XElement(GetCompositionObjectName(obj));
        }
    }
}