﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

using PathGeometry = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Sequence<Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.BezierSegment>;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
#pragma warning disable SA1205 // Partial elements should declare access
#pragma warning disable SA1601 // Partial elements should be documented
    sealed partial class LottieCompositionReader
    {
        static readonly AnimatableParser<double> s_animatableFloatParser = CreateAnimatableParser((in LottieJsonElement el) => el.AsDouble() ?? 0);
        static readonly AnimatableParser<Opacity> s_animatableOpacityParser = CreateAnimatableParser(ParseOpacity);
        static readonly AnimatableParser<PathGeometry> s_animatableGeometryParser = CreateAnimatableParser(ParseGeometry);
        static readonly AnimatableParser<Rotation> s_animatableRotationParser = CreateAnimatableParser(ParseRotation);
        static readonly AnimatableParser<Trim> s_animatableTrimParser = CreateAnimatableParser(ParseTrim);
        static readonly AnimatableParser<Vector3> s_animatableVector3Parser = CreateAnimatableParser(ReadVector3);
        readonly AnimatableParser<Color> _animatableColorParser;

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
            return CreateAnimatable(initialValue, keyFrames, obj.Int32OrNullProperty("ix"));
        }

        Animatable<Color> ReadAnimatableColor(in LottieJsonObjectElement? obj)
        {
            if (obj is null)
            {
                return new Animatable<Color>(Color.Black, null);
            }

            var objValue = obj.Value;
            _animatableColorParser.ParseJson(this, objValue, out var keyFrames, out var initialValue);
            return CreateAnimatable(initialValue, keyFrames, objValue.Int32OrNullProperty("ix"));
        }

        Animatable<double> ReadAnimatableFloat(in LottieJsonObjectElement? obj)
        {
            if (obj is null)
            {
                return new Animatable<double>(0, null);
            }

            var objValue = obj.Value;
            return ReadAnimatable(s_animatableFloatParser, obj.Value);
        }

        Animatable<PathGeometry> ReadAnimatableGeometry(in LottieJsonObjectElement obj)
        {
            s_animatableGeometryParser.ParseJson(this, obj, out var keyFrames, out var initialValue);
            var propertyIndex = obj.Int32OrNullProperty("ix");

            return keyFrames.Any()
                ? new Animatable<PathGeometry>(keyFrames, propertyIndex)
                : new Animatable<PathGeometry>(initialValue, propertyIndex);
        }

        void ReadAnimatableGradientStops(
            in LottieJsonObjectElement obj,
            out Animatable<Sequence<GradientStop>> gradientStops)
        {
            // Get the number of color stops. This is optional unless there are opacity stops.
            // If this value doesn't exist, all the stops are color stops. If the value exists
            // then this is the number of color stops, and the remaining stops are opacity stops.
            var numberOfColorStops = obj.Int32OrNullProperty("p");

            var animatableColorStopsParser = new AnimatableColorStopsParser(numberOfColorStops);
            animatableColorStopsParser.ParseJson(
                this,
                obj.ObjectOrNullProperty("k").Value,
                out var colorKeyFrames,
                out var colorInitialValue);

            var propertyIndex = obj.Int32OrNullProperty("ix");

            if (numberOfColorStops.HasValue)
            {
                // There may be opacity stops. Read them.
                var animatableOpacityStopsParser = new AnimatableOpacityStopsParser(numberOfColorStops.Value);
                animatableOpacityStopsParser.ParseJson(
                    this,
                    obj.ObjectOrNullProperty("k").Value,
                    out var opacityKeyFrames,
                    out var opacityInitialValue);

                if (opacityKeyFrames.Any())
                {
                    // There are opacity key frames. The number of color key frames should be the same
                    // (this is asserted in ConcatGradientStopKeyFrames).
                    gradientStops = new Animatable<Sequence<GradientStop>>(
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

                    gradientStops = new Animatable<Sequence<GradientStop>>(
                                            new Sequence<GradientStop>(colorInitialValue.Concat(opacityInitialValue)),
                                            propertyIndex);
                }
            }
            else
            {
                // There are only color stops.
                gradientStops = colorKeyFrames.Any()
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
                    aKeyFrame.SpatialControlPoint1 != bKeyFrame.SpatialControlPoint1 ||
                    aKeyFrame.SpatialControlPoint2 != bKeyFrame.SpatialControlPoint2 ||
                    aKeyFrame.Easing != bKeyFrame.Easing)
                {
                    throw new LottieCompositionReaderException(
                        "Opacity gradient stop key frame does not match color gradient stop key frame.");
                }

                yield return new KeyFrame<Sequence<GradientStop>>(
                    aKeyFrame.Frame,
                    new Sequence<GradientStop>(aKeyFrame.Value.Concat(bKeyFrame.Value)),
                    aKeyFrame.SpatialControlPoint1,
                    aKeyFrame.SpatialControlPoint2,
                    aKeyFrame.Easing);
            }
        }

        Animatable<Opacity> ReadAnimatableOpacity(in LottieJsonObjectElement obj)
            => ReadAnimatable(s_animatableOpacityParser, obj);

        Animatable<Rotation> ReadAnimatableRotation(in LottieJsonObjectElement obj)
            => ReadAnimatable(s_animatableRotationParser, obj);

        Animatable<Trim> ReadAnimatableTrim(in LottieJsonObjectElement obj)
            => ReadAnimatable(s_animatableTrimParser, obj);

        IAnimatableVector3 ReadAnimatableVector3(in LottieJsonObjectElement obj)
        {
            obj.IgnorePropertyThatIsNotYetSupported("s");

            // Expressions not supported.
            obj.IgnorePropertyThatIsNotYetSupported("x");

            var propertyIndex = obj.Int32OrNullProperty("ix");
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
                var x = ReadAnimatableFloat(obj.ObjectOrNullProperty("x"));
                var y = ReadAnimatableFloat(obj.ObjectOrNullProperty("y"));
                obj.AssertAllPropertiesRead();

                return new AnimatableXYZ(x, y, s_animatable_0);
            }
        }

        sealed class AnimatableColorParser : AnimatableParser<Color>
        {
            readonly bool _ignoreAlpha;

            internal AnimatableColorParser(bool ignoreAlpha)
            {
                _ignoreAlpha = ignoreAlpha;
            }

            protected override Color ReadValue(LottieJsonElement obj)
            {
                var colorArray = obj.AsArray().Value;
                double a = 0;
                double r = 0;
                double g = 0;
                double b = 0;
                int i = 0;
                var count = colorArray.Count;
                for (; i < count; i++)
                {
                    // Note: indexing a JsonArray is faster than enumerating.
                    var jsonValue = colorArray[i];

                    switch (jsonValue.Type)
                    {
                        case JTokenType.Float:
                        case JTokenType.Integer:
                            break;
                        default:
                            if (_ignoreAlpha && i == 3)
                            {
                                // The alpha channel wasn't an expected type, but we are ignoring alpha
                                // so ignore the error.
                                goto AllColorChannelsRead;
                            }

                            throw UnexpectedTokenException(jsonValue.Type);
                    }

                    var number = jsonValue.GetDouble();
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

            protected override Sequence<GradientStop> ReadValue(LottieJsonElement obj)
            {
                var gradientStopsData = obj.AsArray().Value.Select((in LottieJsonElement v) => v.GetDouble()).ToArray();

                // Get the number of color stops. If _colorStopCount wasn't specified, all of
                // the data in the array is for color stops.
                var colorStopsDataLength = _colorStopCount.HasValue
                    ? _colorStopCount.Value * 4
                    : gradientStopsData.Length;

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

            protected override Sequence<GradientStop> ReadValue(LottieJsonElement obj)
            {
                var gradientStopsData = obj.AsArray().Value.Select((in LottieJsonElement v) => v.GetDouble()).Skip(_colorStopCount * 4).ToArray();
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

            protected abstract T ReadValue(LottieJsonElement obj);

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
                                if (k.Type == JTokenType.Array)
                                {
                                    var kArray = k.AsArray().Value;
                                    if (HasKeyframes(kArray))
                                    {
                                        keyFrames = ReadKeyFrames(kArray).ToArray();
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

            static bool HasKeyframes(in LottieJsonArrayElement array)
            {
                var firstItem = array[0];
                return firstItem.Type == JTokenType.Object && firstItem.AsObject()?.ContainsProperty("t") == true;
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
                var ti = default(Vector3);
                var to = default(Vector3);

                // NOTE: indexing an array with GetObjectAt is faster than enumerating.
                for (int i = 0; i < count; i++)
                {
                    var lottieKeyFrame = jsonArray[i].AsObject().Value;

                    // "n" is a name on the keyframe. It is not useful and has been deprecated in Bodymovin.
                    lottieKeyFrame.IgnorePropertyIntentionally("n");

                    // Read the start frame.
                    var startFrame = lottieKeyFrame.DoubleOrNullProperty("t") ?? 0;

                    if (i == count - 1)
                    {
                        // This is the final key frame.
                        // If parsing the old format, this key frame will just have the "t" startFrame value.
                        // If parsing the new format, this key frame will also have the "s" startValue.
                        if (!lottieKeyFrame.TryGetProperty("s", out var finalStartValue))
                        {
                            // Old format.
                            yield return new KeyFrame<T>(startFrame, endValue, to, ti, easing);
                        }
                        else
                        {
                            // New format.
                            yield return new KeyFrame<T>(startFrame, ReadValue(finalStartValue), to, ti, easing);
                        }

                        // No more key frames to read.
                        break;
                    }

                    // Read the start value.
                    lottieKeyFrame.TryGetProperty("s", out var startValueToken);
                    var startValue = ReadValue(startValueToken);

                    // Output a keyframe that describes how to interpolate to this start value. The easing information
                    // comes from the previous Lottie keyframe.
                    yield return new KeyFrame<T>(startFrame, startValue, to, ti, easing);

                    // Spatial control points.
                    if (lottieKeyFrame.ContainsProperty("ti"))
                    {
                        ti = ReadVector3FromJsonArray(lottieKeyFrame.ArrayOrNullProperty("ti"));
                        to = ReadVector3FromJsonArray(lottieKeyFrame.ArrayOrNullProperty("to"));
                    }

                    // Get the easing to the end value, and get the end value.
                    if (lottieKeyFrame.BoolOrNullProperty("h") == true)
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
                        var cp1Json = lottieKeyFrame.ObjectOrNullProperty("o");
                        var cp2Json = lottieKeyFrame.ObjectOrNullProperty("i");
                        if (cp1Json != null && cp2Json != null)
                        {
                            var cp1 = ReadVector3(cp1Json.Value);
                            var cp2 = ReadVector3(cp2Json.Value);
                            easing = new CubicBezierEasing(cp1, cp2);
                        }
                        else
                        {
                            easing = LinearEasing.Instance;
                        }

                        endValue = lottieKeyFrame.TryGetProperty("e", out var endValueObject)
                            ? ReadValue(endValueObject)
                            : default(T);
                    }

                    // "e" is the end value of a key frame but has been deprecated because it should always be equal
                    // to the start value of the next key frame.
                    lottieKeyFrame.IgnorePropertyIntentionally("e");

                    lottieKeyFrame.AssertAllPropertiesRead();
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

            protected override T ReadValue(LottieJsonElement obj) => _valueReader(obj);
        }
    }
}