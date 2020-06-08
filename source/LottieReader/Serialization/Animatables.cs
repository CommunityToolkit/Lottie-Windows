// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        static readonly Sequence<GradientStop> s_defaultGradientStops =
            new Sequence<GradientStop>(new[] { new ColorGradientStop(0, Color.Black) });

        static readonly Animatable<double> s_animatableDoubleZero = CreateNonAnimatedAnimatable(0.0);
        static readonly Animatable<Color> s_animatableColorBlack = CreateNonAnimatedAnimatable(Color.Black);
        static readonly Animatable<Opacity> s_animatableOpacityOpaque = CreateNonAnimatedAnimatable(Opacity.Opaque);
        static readonly Animatable<PathGeometry> s_animatableGeometryEmpty = CreateNonAnimatedAnimatable(PathGeometry.Empty);
        static readonly Animatable<Rotation> s_animatableRotationNone = CreateNonAnimatedAnimatable(Rotation.None);
        static readonly Animatable<Sequence<GradientStop>> s_animatableGradientStopsSingle = CreateNonAnimatedAnimatable(s_defaultGradientStops);
        static readonly Animatable<Trim> s_animatableTrimNone = CreateNonAnimatedAnimatable(Trim.None);
        static readonly AnimatableVector3 s_animatableVector3Zero = new AnimatableVector3(Vector3.Zero, null);
        static readonly AnimatableVector3 s_animatableVector3OneHundred = new AnimatableVector3(new Vector3(100, 100, 100), null);
        static readonly AnimatableParser<double> s_animatableFloatParser =
            CreateAnimatableParser((in LottieJsonElement element) => element.AsDouble() ?? 0);

        static readonly AnimatableParser<Opacity> s_animatableOpacityParser = CreateAnimatableParser(ParseOpacity);
        static readonly AnimatableParser<PathGeometry> s_animatableGeometryParser = CreateAnimatableParser(ParseGeometry);
        static readonly AnimatableParser<Rotation> s_animatableRotationParser = CreateAnimatableParser(ParseRotation);
        static readonly AnimatableParser<Trim> s_animatableTrimParser = CreateAnimatableParser(ParseTrim);
        static readonly AnimatableParser<Vector3> s_animatableVector3Parser = CreateAnimatableParser(ParseVector3);
        readonly AnimatableParser<Color> _animatableColorParser;

        static Animatable<T> CreateNonAnimatedAnimatable<T>(T value)
            where T : IEquatable<T>
            => new Animatable<T>(value, null);

        static Animatable<T> CreateAnimatable<T>(T initialValue, IEnumerable<KeyFrame<T>> keyFrames, int? propertyIndex)
            where T : IEquatable<T>
            => keyFrames.Any()
                ? new Animatable<T>(keyFrames, propertyIndex)
                : new Animatable<T>(initialValue, propertyIndex);

        static SimpleAnimatableParser<T> CreateAnimatableParser<T>(LottieJsonElementReader<T> valueReader)
            where T : IEquatable<T>
             => new SimpleAnimatableParser<T>(valueReader);

        Animatable<T> ReadAnimatable<T>(AnimatableParser<T> parser, in LottieJsonObjectElement obj)
            where T : IEquatable<T>
        {
            parser.ParseJson(this, obj, out var keyFrames, out var initialValue);
            return CreateAnimatable(initialValue, keyFrames, obj.Int32PropertyOrNull("ix"));
        }

        Animatable<Color> ReadAnimatableColor(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableColorBlack
                : ReadAnimatable(_animatableColorParser, obj.Value);

        Animatable<double> ReadAnimatableFloat(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableDoubleZero
                : ReadAnimatable(s_animatableFloatParser, obj.Value);

        Animatable<PathGeometry> ReadAnimatableGeometry(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableGeometryEmpty
                : ReadAnimatable(s_animatableGeometryParser, obj.Value);

        Animatable<Sequence<GradientStop>> ReadAnimatableGradientStops(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableGradientStopsSingle
                : ReadAnimatableGradientStops(obj.Value);

        Animatable<Sequence<GradientStop>> ReadAnimatableGradientStops(in LottieJsonObjectElement obj)
        {
            // If the "k" doesn't exist, we can't parse.
            var kObj = obj.ObjectPropertyOrNull("k");

            if (kObj is null)
            {
                return s_animatableGradientStopsSingle;
            }
            else
            {
                // Get the number of color stops. This is optional unless there are opacity stops.
                // If this value doesn't exist, all the stops are color stops. If the value exists
                // then this is the number of color stops, and the remaining stops are opacity stops.
                var numberOfColorStops = obj.Int32PropertyOrNull("p");
                var propertyIndex = obj.Int32PropertyOrNull("ix");
                return ReadAnimatableGradientStops(kObj.Value, numberOfColorStops, propertyIndex);
            }
        }

        Animatable<Sequence<GradientStop>> ReadAnimatableGradientStops(
                in LottieJsonObjectElement obj,
                int? numberOfColorStops,
                int? propertyIndex)
        {
            var animatableColorStopsParser = new AnimatableColorStopsParser(numberOfColorStops);

            animatableColorStopsParser.ParseJson(
                this,
                obj,
                out var colorKeyFrames,
                out var colorInitialValue);

            if (numberOfColorStops.HasValue)
            {
                // There may be opacity stops. Read them.
                var animatableOpacityStopsParser = new AnimatableOpacityStopsParser(numberOfColorStops.Value);
                animatableOpacityStopsParser.ParseJson(
                    this,
                    obj,
                    out var opacityKeyFrames,
                    out var opacityInitialValue);

                if (opacityKeyFrames.Any())
                {
                    // There are opacity key frames. The number of color key frames should be the same
                    // (this is asserted in ConcatGradientStopKeyFrames).
                    return new Animatable<Sequence<GradientStop>>(
                                            ConcatGradientStopKeyFrames(colorKeyFrames, opacityKeyFrames),
                                            propertyIndex);
                }
                else
                {
                    // There is only an initial opacity value (i.e. no key frames).
                    // There should be no key frames for color either.
                    if (colorKeyFrames.Any())
                    {
                        throw new LottieCompositionReaderException(
                            "Numbers of key frames in opacity gradient stops and color gradient stops are unequal.");
                    }

                    return new Animatable<Sequence<GradientStop>>(
                                            new Sequence<GradientStop>(colorInitialValue.Concat(opacityInitialValue)),
                                            propertyIndex);
                }
            }
            else
            {
                // There are only color stops.
                return colorKeyFrames.Any()
                    ? new Animatable<Sequence<GradientStop>>(colorKeyFrames, propertyIndex)
                    : new Animatable<Sequence<GradientStop>>(colorInitialValue, propertyIndex);
            }
        }

        static IEnumerable<KeyFrame<Sequence<GradientStop>>> ConcatGradientStopKeyFrames(
            IEnumerable<KeyFrame<Sequence<GradientStop>>> a,
            IEnumerable<KeyFrame<Sequence<GradientStop>>> b)
        {
            var aArray = a.ToArray();
            var bArray = b.ToArray();
            if (aArray.Length != bArray.Length)
            {
                throw new LottieCompositionReaderException(
                    "Numbers of key frames in opacity gradient stops and color gradient stops are unequal.");
            }

            for (var i = 0; i < aArray.Length; i++)
            {
                var aKeyFrame = aArray[i];
                var bKeyFrame = bArray[i];

                if (aKeyFrame.Frame != bKeyFrame.Frame ||
                    aKeyFrame.SpatialBezier != bKeyFrame.SpatialBezier ||
                    aKeyFrame.Easing != bKeyFrame.Easing)
                {
                    throw new LottieCompositionReaderException(
                        "Opacity gradient stop key frame does not match color gradient stop key frame.");
                }

                yield return aKeyFrame.CloneWithNewValue(new Sequence<GradientStop>(aKeyFrame.Value.Concat(bKeyFrame.Value)));
            }
        }

        Animatable<Opacity> ReadAnimatableOpacity(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableOpacityOpaque
                : ReadAnimatable(s_animatableOpacityParser, obj.Value);

        Animatable<Rotation> ReadAnimatableRotation(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableRotationNone
                : ReadAnimatable(s_animatableRotationParser, obj.Value);

        Animatable<Trim> ReadAnimatableTrim(in LottieJsonObjectElement? obj)
            => obj is null
                ? s_animatableTrimNone
                : ReadAnimatable(s_animatableTrimParser, obj.Value);

        IAnimatableVector3 ReadAnimatableVector3(in LottieJsonObjectElement? obj, IAnimatableVector3 defaultValue)
            => obj is null
                ? defaultValue
                : ReadAnimatableVector3(obj.Value);

        IAnimatableVector3 ReadAnimatableVector3(in LottieJsonObjectElement? obj)
            => ReadAnimatableVector3(obj, s_animatableVector3Zero);

        IAnimatableVector3 ReadAnimatableVector3(in LottieJsonObjectElement obj)
        {
            obj.IgnorePropertyThatIsNotYetSupported("s");

            // Expressions not supported.
            obj.IgnorePropertyThatIsNotYetSupported("x");

            var propertyIndex = obj.Int32PropertyOrNull("ix");
            if (obj.ContainsProperty("k"))
            {
                s_animatableVector3Parser.ParseJson(this, obj, out var keyFrames, out var initialValue);
                obj.AssertAllPropertiesRead();

                return keyFrames.Any()
                    ? new AnimatableVector3(keyFrames, propertyIndex)
                    : new AnimatableVector3(initialValue, propertyIndex);
            }
            else
            {
                // Split X and Y dimensions
                var x = ReadAnimatableFloat(obj.ObjectPropertyOrNull("x"));
                var y = ReadAnimatableFloat(obj.ObjectPropertyOrNull("y"));
                obj.AssertAllPropertiesRead();

                return new AnimatableXYZ(x, y, s_animatableDoubleZero);
            }
        }

        sealed class AnimatableColorParser : AnimatableParser<Color>
        {
            readonly bool _ignoreAlpha;

            internal AnimatableColorParser(bool ignoreAlpha)
            {
                _ignoreAlpha = ignoreAlpha;
            }

            protected override Color ReadValue(in LottieJsonElement element)
            {
                var array = element.AsArray();

                // Colors are expected to be arrays.
                // If we can't parse it at least return a color.
                return array is null
                    ? Color.Black
                    : ReadValue(array.Value);
            }

            Color ReadValue(in LottieJsonArrayElement array)
            {
                double a = 0;
                double r = 0;
                double g = 0;
                double b = 0;
                int i = 0;
                var count = array.Count;
                for (; i < count; i++)
                {
                    // Note: indexing a JsonArray is faster than enumerating.
                    var jsonValue = array[i];

                    switch (jsonValue.Kind)
                    {
                        case JsonValueKind.Number:
                            break;
                        default:
                            if (_ignoreAlpha && i == 3)
                            {
                                // The alpha channel wasn't an expected type, but we are ignoring alpha
                                // so ignore the error.
                                goto AllColorChannelsRead;
                            }

                            throw Exception($"Unexpected {jsonValue.Kind}");
                    }

                    var number = jsonValue.AsDouble() ?? 0;
                    switch (i)
                    {
                        case 0:
                            r = number;
                            break;
                        case 1:
                            g = number;
                            break;
                        case 2:
                            b = number;
                            break;
                        case 3:
                            a = number;
                            break;
                    }
                }

            AllColorChannelsRead:

                // Treat any missing values as 0.
                // Some versions of Lottie use floats, some use bytes. Assume bytes if any values are > 1.
                if (r > 1 || g > 1 || b > 1 || a > 1)
                {
                    // Convert byte to float.
                    a /= 255;
                    r /= 255;
                    g /= 255;
                    b /= 255;
                }

                return Color.FromArgb(_ignoreAlpha ? 1 : a, r, g, b);
            }
        }

        sealed class AnimatableColorStopsParser : AnimatableParser<Sequence<GradientStop>>
        {
            // The number of color stops. The opacity stops follow this number
            // of color stops. If not specified, all of the values are color stops.
            readonly int? _colorStopCount;

            internal AnimatableColorStopsParser(int? colorStopCount)
            {
                _colorStopCount = colorStopCount;
            }

            protected override Sequence<GradientStop> ReadValue(in LottieJsonElement element)
            {
                var array = element.AsArray();
                return array is null
                    ? s_defaultGradientStops
                    : ReadValue(array.Value);
            }

            Sequence<GradientStop> ReadValue(in LottieJsonArrayElement array)
            {
                var gradientStopsData = array.Select((in LottieJsonElement v) => v.AsDouble() ?? 0).ToArray();

                // Get the number of color stops. If _colorStopCount wasn't specified, all of
                // the data in the array is for color stops.
                var colorStopsDataLength = _colorStopCount * 4 ?? gradientStopsData.Length;

                if (gradientStopsData.Length < colorStopsDataLength)
                {
                    throw new LottieCompositionReaderException("Fewer gradient stop values than expected");
                }

                var colorStopsCount = colorStopsDataLength / 4;
                var colorStops = new ColorGradientStop[colorStopsCount];

                var offset = 0.0;
                var red = 0.0;
                var green = 0.0;
                int i;
                for (i = 0; i < colorStopsDataLength; i++)
                {
                    var value = gradientStopsData[i];
                    switch (i % 4)
                    {
                        case 0:
                            offset = value;
                            break;
                        case 1:
                            red = value;
                            break;
                        case 2:
                            green = value;
                            break;
                        case 3:
                            var blue = value;

                            // Some versions of Lottie use floats, some use bytes. Assume bytes if any values are > 1.
                            if (red > 1 || green > 1 || blue > 1)
                            {
                                // Convert byte to float.
                                red /= 255;
                                green /= 255;
                                blue /= 255;
                            }

                            colorStops[i / 4] = new ColorGradientStop(offset, Color.FromArgb(1, red, green, blue));
                            break;
                    }
                }

                return new Sequence<GradientStop>(colorStops);
            }
        }

        sealed class AnimatableOpacityStopsParser : AnimatableParser<Sequence<GradientStop>>
        {
            // The number of color stops. The opacity stops follow this number of color stops.
            readonly int _colorStopCount;

            internal AnimatableOpacityStopsParser(int colorStopCount)
            {
                _colorStopCount = colorStopCount;
            }

            protected override Sequence<GradientStop> ReadValue(in LottieJsonElement element)
            {
                var array = element.AsArray();
                return array is null
                    ? s_defaultGradientStops
                    : ReadValue(array.Value);
            }

            Sequence<GradientStop> ReadValue(in LottieJsonArrayElement array)
            {
                var gradientStopsData = array.Select((in LottieJsonElement v) => v.AsDouble() ?? 0).Skip(_colorStopCount * 4).ToArray();
                var gradientStops = new OpacityGradientStop[gradientStopsData.Length / 2];
                var offset = 0.0;
                for (int i = 0; i < gradientStopsData.Length; i++)
                {
                    var value = gradientStopsData[i];
                    switch (i % 2)
                    {
                        case 0:
                            offset = value;
                            break;
                        case 1:
                            double opacity = value;

                            // Some versions of Lottie use floats, some use bytes. Assume bytes if any values are > 1.
                            if (opacity > 1)
                            {
                                // Convert byte to float.
                                opacity /= 255;
                            }

                            gradientStops[i / 2] = new OpacityGradientStop(offset, opacity: Opacity.FromFloat(opacity));
                            break;
                    }
                }

                return new Sequence<GradientStop>(gradientStops);
            }
        }

        abstract class AnimatableParser<T>
            where T : IEquatable<T>
        {
            private protected AnimatableParser()
            {
            }

            protected abstract T ReadValue(in LottieJsonElement element);

            internal void ParseJson(
                LottieCompositionReader reader,
                in LottieJsonObjectElement obj,
                out IEnumerable<KeyFrame<T>> keyFrames,
                out T initialValue)
            {
                // Deprecated "a" property meant "isAnimated". The existence of key frames means the same thing.
                obj.IgnorePropertyIntentionally("a");

                keyFrames = Array.Empty<KeyFrame<T>>();
                initialValue = default(T);

                foreach (var property in obj)
                {
                    switch (property.Key)
                    {
                        case "k":
                            {
                                var k = property.Value;
                                if (k.Kind == JsonValueKind.Array)
                                {
                                    var kArray = k.AsArray();
                                    if (HasKeyframes(kArray))
                                    {
                                        keyFrames = ReadKeyFrames(kArray.Value).ToArray();
                                        initialValue = keyFrames.First().Value;
                                    }
                                }

                                if (keyFrames == Array.Empty<KeyFrame<T>>())
                                {
                                    initialValue = ReadValue(k);
                                }
                            }

                            break;

                        // Defines if property is animated. 0 or 1.
                        // Currently ignored because we derive this from the existence of keyframes.
                        case "a":
                            break;

                        // Property index. Used for expressions. Currently ignored because we don't support expressions.
                        case "ix":
                            // Do not report it as an issue - existence of "ix" doesn't mean that an expression is actually used.
                            break;

                        // Extremely rare properties seen in 1 Lottie file. Ignore.
                        case "nm": // Name
                        case "mn": // MatchName
                        case "hd": // IsHidden
                            break;

                        // Property expression. Currently ignored because we don't support expressions.
                        case "x":
                            reader._issues.Expressions();
                            break;
                        default:
                            reader._issues.UnexpectedField(property.Key);
                            break;
                    }
                }
            }

            static bool HasKeyframes(in LottieJsonArrayElement? array)
            {
                return array?[0].AsObject()?.ContainsProperty("t") == true;
            }

            IEnumerable<KeyFrame<T>> ReadKeyFrames(
                LottieJsonArrayElement jsonArray)
            {
                int count = jsonArray.Count;

                if (count == 0)
                {
                    yield break;
                }

                // -
                // Keyframes are encoded in Lottie as an array consisting of a sequence
                // of start value with start frame and easing function. The final entry in the
                // array is the frame at which the last interpolation ends.
                // [
                //   { startValue_1, startFrame_1 },  # interpolates from startValue_1 to startValue_2 from startFrame_1 to startFrame_2
                //   { startValue_2, startFrame_2 },  # interpolates from startValue_2 to startValue_3 from startFrame_2 to startFrame_3
                //   { startValue_3, startFrame_3 },  # interpolates from startValue_3 to startValue_4 from startFrame_3 to startFrame_4
                //   { startValue_4, startFrame_4 }
                // ]
                // Earlier versions of Bodymovin used an endValue in each key frame.
                // [
                //   { startValue_1, endValue_1, startFrame_1 },  # interpolates from startValue_1 to endValue_1 from startFrame_1 to startFrame_2
                //   { startValue_2, endValue_2, startFrame_2 },  # interpolates from startValue_2 to endValue_2 from startFrame_2 to startFrame_3
                //   { startValue_3, endValue_3, startFrame_3 },  # interpolates from startValue_3 to endValue_3 from startFrame_3 to startFrame_4
                //   { startFrame_4 }
                // ]
                //
                // In order to handle the current and old formats, we detect the presence of the endValue property.
                // If there's an endValue property, the keyframes are using the old format.
                //
                // We convert these to keyframes that match the Windows.UI.Composition notion of a keyframe,
                // which is a triple: {endValue, endTime, easingFunction}.
                // An initial keyframe is created to describe the initial value. It has no easing function.
                //
                // -
                T endValue = default(T);

                // The initial keyframe has the same value as the initial value. Easing therefore doesn't
                // matter, but might as well use hold as it's the simplest (it does not interpolate).
                Easing easing = HoldEasing.Instance;

                // SpatialBeziers.
                var ti = default(Vector2);
                var to = default(Vector2);

                // NOTE: indexing an array with GetObjectAt is faster than enumerating.
                for (int i = 0; i < count; i++)
                {
                    var lottieKeyFrame = jsonArray[i].AsObject();
                    if (lottieKeyFrame is null)
                    {
                        throw Exception($"Unexpected {jsonArray[i].Kind}");
                    }

                    var lottieKeyFrameObj = lottieKeyFrame.Value;

                    // "n" is a name on the keyframe. It is not useful and has been deprecated in Bodymovin.
                    lottieKeyFrameObj.IgnorePropertyIntentionally("n");

                    // Read the start frame.
                    var startFrame = lottieKeyFrameObj.DoublePropertyOrNull("t") ?? 0;

                    var spatialBezier = new CubicBezier(to, ti);

                    if (i == count - 1)
                    {
                        // This is the final key frame.
                        // If parsing the old format, this key frame will just have the "t" startFrame value.
                        // If parsing the new format, this key frame will also have the "s" startValue.
                        if (!lottieKeyFrameObj.TryGetProperty("s", out var finalStartValue))
                        {
                            // Old format.
                            yield return new KeyFrame<T>(startFrame, endValue, spatialBezier, easing);
                        }
                        else
                        {
                            // New format.
                            yield return new KeyFrame<T>(startFrame, ReadValue(finalStartValue), spatialBezier, easing);
                        }

                        // No more key frames to read.
                        break;
                    }

                    // Read the start value.
                    lottieKeyFrameObj.TryGetProperty("s", out var startValueToken);
                    var startValue = ReadValue(startValueToken);

                    // Output a keyframe that describes how to interpolate to this start value. The easing information
                    // comes from the previous Lottie keyframe.
                    yield return new KeyFrame<T>(startFrame, startValue, spatialBezier, easing);

                    // Spatial control points.
                    if (lottieKeyFrameObj.ContainsProperty("ti"))
                    {
                        ti = lottieKeyFrameObj.ArrayPropertyOrNull("ti")?.AsVector2() ?? Vector2.Zero;
                        to = lottieKeyFrameObj.ArrayPropertyOrNull("to")?.AsVector2() ?? Vector2.Zero;
                    }

                    // Get the easing to the end value, and get the end value.
                    if (lottieKeyFrameObj.BoolPropertyOrNull("h") == true)
                    {
                        // Hold the current value. The next value comes from the start
                        // of the next entry.
                        easing = HoldEasing.Instance;

                        // Synthesize an endValue. This is only used if this is the final frame.
                        endValue = startValue;
                    }
                    else
                    {
                        // Read the easing function parameters. If there are any parameters, it's a CubicBezierEasing.
                        var cp1Json = lottieKeyFrameObj.ObjectPropertyOrNull("o");
                        var cp2Json = lottieKeyFrameObj.ObjectPropertyOrNull("i");
                        if (cp1Json != null && cp2Json != null)
                        {
                            var cp1 = cp1Json.Value.AsVector2();
                            var cp2 = cp2Json.Value.AsVector2();
                            easing = new CubicBezierEasing(new[] { new CubicBezier(cp1 ?? Vector2.Zero, cp2 ?? Vector2.Zero) });
                        }
                        else
                        {
                            easing = LinearEasing.Instance;
                        }

                        endValue = lottieKeyFrameObj.TryGetProperty("e", out var endValueObject)
                            ? ReadValue(endValueObject)
                            : default(T);
                    }

                    // "e" is the end value of a key frame but has been deprecated because it should always be equal
                    // to the start value of the next key frame.
                    lottieKeyFrameObj.IgnorePropertyIntentionally("e");

                    lottieKeyFrameObj.AssertAllPropertiesRead();
                }
            }
        }

        // An AnimatableParser that does not need to hold any state, and for which the ReadValue
        // method can be easily expressed as a lambda.
        sealed class SimpleAnimatableParser<T> : AnimatableParser<T>
            where T : IEquatable<T>
        {
            readonly LottieJsonElementReader<T> _valueReader;

            internal SimpleAnimatableParser(LottieJsonElementReader<T> valueReader)
            {
                _valueReader = valueReader;
            }

            protected override T ReadValue(in LottieJsonElement element) => _valueReader(element);
        }
    }
}