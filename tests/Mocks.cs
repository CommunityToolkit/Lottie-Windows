// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file contains mocks of WinUI and Win2D for use when testing
// that LottieGen's output is compilable.
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Graphics;
using Windows.Graphics.Effects;
using Windows.UI.Composition;

namespace Microsoft.UI.Xaml.Controls
{
    interface IAnimatedVisual : IDisposable
    {
        Visual RootVisual { get; }
        TimeSpan Duration { get; }
        Vector2 Size { get; }
    }

    interface IAnimatedVisualSource
    {
        IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics);
    }
}

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
    }

    class CanvasGeometry : IGeometrySource2D
    {
        public static CanvasGeometry CreateGroup(object arg0, CanvasGeometry[] arg1) => null;
        public static CanvasGeometry CreateGroup(object arg0, CanvasGeometry[] arg1, CanvasFilledRegionDetermination arg3) => null;
        public static CanvasGeometry CreatePath(CanvasPathBuilder builder) => null;
        public CanvasGeometry CombineWith(CanvasGeometry arg) => null;
        public CanvasGeometry CombineWith(CanvasGeometry otherGeometry, Matrix3x2 otherGeometryTransform, CanvasGeometryCombine combine) => null;
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
