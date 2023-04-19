// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using CommunityToolkit.WinUI.Lottie.WinCompData.Mgc;

namespace CommunityToolkit.WinUI.Lottie.WinCompData.Mgcg
{
#if PUBLIC_WinCompData
    public
#endif
    sealed class CanvasPathBuilder : IDisposable
    {
        readonly List<Command> _commands = new List<Command>();
        bool _isFilledRegionDeterminationSet;

        public CanvasPathBuilder(CanvasDevice? device)
        {
        }

        public void BeginFigure(Vector2 startPoint)
        {
            _commands.Add(new Command.BeginFigure(startPoint));
        }

        public void EndFigure(CanvasFigureLoop figureLoop)
        {
            _commands.Add(new Command.EndFigure(figureLoop));
        }

        public void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
        {
            _commands.Add(new Command.AddCubicBezier(controlPoint1, controlPoint2, endPoint));
        }

        public void AddLine(Vector2 endPoint)
        {
            _commands.Add(new Command.AddLine(endPoint));
        }

        public void SetFilledRegionDetermination(CanvasFilledRegionDetermination value)
        {
            if (_isFilledRegionDeterminationSet)
            {
                // Throw if someone attempts to set the CanvasFilledRegionDetermination twice.
                // We could handle it, but it almost certainly indicates an accidental use
                // of the API.
                throw new InvalidOperationException();
            }

            _isFilledRegionDeterminationSet = true;
            FilledRegionDetermination = value;
        }

        internal CanvasFilledRegionDetermination FilledRegionDetermination { get; private set; }

        internal IEnumerable<Command> Commands => _commands;

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        public abstract class Command : IEquatable<Command>
        {
            Command()
            {
            }

            public abstract CommandType Type { get; }

            /// <inheritdoc/>
            public bool Equals(Command? other)
            {
                if (other is null)
                {
                    return false;
                }

                if (other.Type != Type)
                {
                    return false;
                }

                return Type switch
                {
                    CommandType.BeginFigure => ((BeginFigure)this).Equals((BeginFigure)other),
                    CommandType.EndFigure => ((EndFigure)this).Equals((EndFigure)other),
                    CommandType.AddCubicBezier => ((AddCubicBezier)this).Equals((AddCubicBezier)other),
                    CommandType.AddLine => ((AddLine)this).Equals((AddLine)other),
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                    _ => throw new InvalidOperationException(),
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
                };
            }

            public sealed class BeginFigure : Command, IEquatable<BeginFigure>
            {
                internal BeginFigure(Vector2 startPoint)
                {
                    StartPoint = startPoint;
                }

                /// <inheritdoc/>
                public override CommandType Type => CommandType.BeginFigure;

                public Vector2 StartPoint { get; }

                /// <inheritdoc/>
                public bool Equals(BeginFigure? other) => other is not null && other.StartPoint.Equals(StartPoint);
            }

            public sealed class EndFigure : Command, IEquatable<EndFigure>
            {
                internal EndFigure(CanvasFigureLoop figureLoop)
                {
                    FigureLoop = figureLoop;
                }

                /// <inheritdoc/>
                public override CommandType Type => CommandType.EndFigure;

                public CanvasFigureLoop FigureLoop { get; }

                /// <inheritdoc/>
                public bool Equals(EndFigure? other) => other is not null && other.FigureLoop == FigureLoop;
            }

            public sealed class AddCubicBezier : Command, IEquatable<AddCubicBezier>
            {
                internal AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
                {
                    ControlPoint1 = controlPoint1;
                    ControlPoint2 = controlPoint2;
                    EndPoint = endPoint;
                }

                /// <inheritdoc/>
                public override CommandType Type => CommandType.AddCubicBezier;

                public Vector2 ControlPoint1 { get; }

                public Vector2 ControlPoint2 { get; }

                public Vector2 EndPoint { get; }

                /// <inheritdoc/>
                public bool Equals(AddCubicBezier? other) =>
                    other is not null &&
                    other.ControlPoint1.Equals(ControlPoint1) &&
                    other.ControlPoint2.Equals(ControlPoint2) &&
                    other.EndPoint.Equals(EndPoint);
            }

            public sealed class AddLine : Command, IEquatable<AddLine>
            {
                internal AddLine(Vector2 endPoint)
                {
                    EndPoint = endPoint;
                }

                /// <inheritdoc/>
                public override CommandType Type => CommandType.AddLine;

                public Vector2 EndPoint { get; }

                /// <inheritdoc/>
                public bool Equals(AddLine? other) => other is not null && other.EndPoint.Equals(EndPoint);
            }
        }

        public enum CommandType
        {
            BeginFigure,
            EndFigure,
            AddCubicBezier,
            AddLine,
        }
    }
}
