// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file contains mocks of Win2D for use when testing that LottieGen's output is compilable.
using System;
using System.Collections.Generic;
using System.Numerics;
//using System.Numerics.Vectors;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Effects;
using Windows.UI.Composition;

namespace Microsoft.Graphics.Canvas
{
    enum CanvasComposite
    {
        DestinationIn,
        DestinationOut,
    }
}

namespace Microsoft.Graphics.Canvas.Effects
{
    class CompositeEffect : Windows.Graphics.Effects.IGraphicsEffect
    {
        public CanvasComposite Mode { get; set; }
        public string Name { get; set; }
        public IList<IGraphicsEffectSource> Sources { get; }
    }
}

namespace Microsoft.Graphics.Canvas.Geometry
{
    enum CanvasFigureLoop
    {
        Open,
        Closed,
    }

    enum CanvasFilledRegionDetermination
    {
        Winding,
        Alternate,
    }

    enum CanvasGeometryCombine
    {
        Intersect,
        Xor,
        Exclude,
        Union,
    }

    class CanvasGeometry : IGeometrySource2D
    {
        public static CanvasGeometry CreateEllipse(object arg0, float arg1, float arg2, float arg3, float arg4) => null;
        public static CanvasGeometry CreateGroup(object arg0, CanvasGeometry[] arg1) => null;
        public static CanvasGeometry CreateGroup(object arg0, CanvasGeometry[] arg1, CanvasFilledRegionDetermination arg3) => null;
        public static CanvasGeometry CreatePath(CanvasPathBuilder builder) => null;
        public static CanvasGeometry CreateRoundedRectangle(object arg0, float arg1, float arg2, float arg3, float arg4, float arg5, float arg6);
        public CanvasGeometry CombineWith(CanvasGeometry arg) => null;
        public CanvasGeometry CombineWith(CanvasGeometry otherGeometry, Matrix3x2 otherGeometryTransform, CanvasGeometryCombine combine) => null;
        public CanvasGeometry Transform(Matrix3x2 transform) => null;
    }

    class CanvasPathBuilder : IDisposable
    {
        public CanvasPathBuilder(object device) { }
        public void AddCubicBezier(Vector2 arg0, Vector2 arg1, Vector2 arg2) { }
        public void AddLine(Vector2 value) { }
        public void BeginFigure(Vector2 arg) { }
        public void EndFigure(CanvasFigureLoop arg) { }
        public void SetFilledRegionDetermination(CanvasFilledRegionDetermination arg) { }
        void IDisposable.Dispose() => throw new NotImplementedException();
    }
}
