// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable // Temporary while enabling nullable everywhere.

using System;
using System.Collections.Specialized;
using System.Numerics;
using LottieViewer.ViewModel;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace LottieViewer
{
#pragma warning disable SA1303 // Constants must begin with an upper case letter.
#pragma warning disable SA1402 // File may only contain a single type.

    /// <summary>
    /// A slider-like control for displaying and setting the position of a Lottie animation. Also
    /// displays any markers that are part of the Lottie animation.
    /// </summary>
    public sealed partial class Scrubber : UserControl
    {
        // Set this to 0 for production. Offsets the Composition pieces to make them
        // easier to see when the slider is opaque.
        const float c_verticalOffset = 0;

        // These values should not be changed - some of the values are hard coded and
        // assume the current values.
        const float c_thumbRadius = 9;
        const float c_thumbStrokeThickness = 2;
        const float c_trackWidth = 2;

        // The margin on the left and right side of the margin.
        const float c_trackMargin = 9;

        readonly CompositionPropertySet _properties;
        readonly SpriteVisual _decreaseRectangle;
        readonly SpriteVisual _trackRectangle;
        readonly ShapeVisual _thumb;
        readonly CompositionColorBrush _trackRectangleBrush;
        readonly CompositionColorBrush _decreaseRectangleBrush;
        readonly SolidColorBrush _markerBrush;

        LottieVisualDiagnosticsViewModel _diagnostics;

        string _currentVisualStateName;

        public event TypedEventHandler<Scrubber, ScrubberValueChangedEventArgs> ValueChanged;

        internal LottieVisualDiagnosticsViewModel DiagnosticsViewModel
        {
            get => _diagnostics;
            set
            {
                if (_diagnostics != null)
                {
                    _diagnostics.Markers.CollectionChanged -= Markers_CollectionChanged;
                }

                _diagnostics = value;
                _diagnostics.Markers.CollectionChanged += Markers_CollectionChanged;
            }
        }

        public Scrubber()
        {
            this.InitializeComponent();

            // Create the brush used for markers.
            _markerBrush = new SolidColorBrush(GetResourceBrushColor("LottieBasicBrush"));

            // Set our tooltip converter so we are in charge of what is shown on the tooltip.
            _slider.ThumbToolTipValueConverter = new ThumbTooltipConverter(this);

            // Forward the slider change events to any event listeners.
            _slider.ValueChanged += (sender, e) => ValueChanged?.Invoke(this, new ScrubberValueChangedEventArgs(e.OldValue, e.NewValue));

            // Set up the Windows.UI.Composition pieces that will display the parts of the slider.
            // The XAML slider has its parts set to Opacity=0 so they aren't visible and are
            // only used to handle user input.
            var c = Window.Current.Compositor;

            var container = c.CreateContainerVisual();

            // Get the property set. This will be used to animate the trackbar and thumb.
            _properties = container.Properties;

            // Add a property to scale the width of the track and decrease rectangles.
            _properties.InsertScalar("Width", default);

            // Create the brushes and set up expression animations so their colors can
            // be changed by writing to the property set.
            var thumbFillBrush = CreateBoundColorBrush(_properties, "ThumbFillColor");
            var thumbStrokeBrush = CreateBoundColorBrush(_properties, "ThumbStrokeColor");
            _trackRectangleBrush = CreateBoundColorBrush(_properties, "TrackColor");
            _decreaseRectangleBrush = CreateBoundColorBrush(_properties, "DecreaseRectangleColor");

            // Create the track rectangle. This is the track that the thumb moves along.
            _trackRectangle = c.CreateSpriteVisual();
            _trackRectangle.Size = new Vector2(0, c_trackWidth);
            _trackRectangle.Brush = _trackRectangleBrush;
            container.Children.InsertAtTop(_trackRectangle);

            // Create the decrease rectangle. This is the rectangle that will change in width as the
            // slider position is changed. It shows on the left side of the thumb to indicate how
            // far along the track the position is.
            _decreaseRectangle = c.CreateSpriteVisual();
            _decreaseRectangle.Size = new Vector2(0, c_trackWidth);
            _decreaseRectangle.Brush = _decreaseRectangleBrush;
            container.Children.InsertAtTop(_decreaseRectangle);

            // Move the decrease and track rectangles into the track of the slider.
            _trackRectangle.Offset = new Vector3(9, 15.5F + c_verticalOffset, 0);
            _decreaseRectangle.Offset = new Vector3(9, 15.5F + c_verticalOffset, 0);

            // Create the thumb.
            _thumb = c.CreateShapeVisual();
            var thumbEllipse = c.CreateEllipseGeometry();
            thumbEllipse.Radius = new Vector2(c_thumbRadius);
            thumbEllipse.Center = new Vector2(c_thumbRadius + c_thumbStrokeThickness);

            var thumbShape = c.CreateSpriteShape(thumbEllipse);
            thumbShape.FillBrush = thumbFillBrush;
            thumbShape.StrokeBrush = thumbStrokeBrush;
            thumbShape.StrokeThickness = c_thumbStrokeThickness;
            _thumb.Shapes.Add(thumbShape);
            _thumb.Size = new Vector2((c_thumbRadius + c_thumbStrokeThickness) * 2);

            // X value doesn't matter for Offset because it is controlled by an expression animation.
            // The Y value is used to position the thumb vertically so it is centered over the track.
            _thumb.Offset = new Vector3(0, c_thumbRadius + c_verticalOffset - 3.75F, 0);
            container.Children.InsertAtTop(_thumb);

            // Attach our custom-drawn UI as a child visual of the slider.
            ElementCompositionPreview.SetElementChildVisual(_slider, container);

            // Set the initial colors.
            UpdateColors();
        }

        /// <summary>
        /// The current value of the scrubber. Value from 0 to 1.
        /// </summary>
        public double Value
        {
            get => _slider.Value;
            set => _slider.Value = value;
        }

        // Associates the given CompositionObject with the scrubber. The object is required
        // to have a property called "Progress" that the scrubber position will be bound to.
        internal void SetAnimatedCompositionObject(CompositionObject obj)
        {
            var c = Window.Current.Compositor;

            var decreaseRectAnimation = c.CreateExpressionAnimation($"comp.Progress * (our.Width - {c_trackMargin * 2})");
            decreaseRectAnimation.SetReferenceParameter("comp", obj);
            decreaseRectAnimation.SetReferenceParameter("our", _properties);
            _decreaseRectangle.StartAnimation("Size.X", decreaseRectAnimation);

            var trackRectangleAnimation = c.CreateExpressionAnimation($"our.Width - {c_trackMargin * 2}");
            trackRectangleAnimation.SetReferenceParameter("comp", obj);
            trackRectangleAnimation.SetReferenceParameter("our", _properties);
            _trackRectangle.StartAnimation("Size.X", trackRectangleAnimation);

            var thumbPositionAnimation = c.CreateExpressionAnimation($"comp.Progress * (our.Width - {(c_thumbRadius + c_thumbStrokeThickness) * 1.78}) - 1");
            thumbPositionAnimation.SetReferenceParameter("comp", obj);
            thumbPositionAnimation.SetReferenceParameter("our", _properties);
            _thumb.StartAnimation("Offset.X", thumbPositionAnimation);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Arrange the elements. This has to be done before asking the
            // slider for its size.
            var result = base.ArrangeOverride(finalSize);

            var sliderWidth = _slider.ActualWidth;

            // Update the size of the progress rectangle and the position of the thumb.
            _properties.InsertScalar("Width", (float)sliderWidth);

            // Update the position of the markers.
            // Subtract 1 to the width because we need to allow for a marker at offset 1, and
            // the markers are 1 pixel wide.
            var barWidth = sliderWidth - 1 - (c_trackMargin * 2);

            // Adjust the position of the markers.
            // Set the margin on each of the rectangles in the grid so that they match
            // the offsets of the markers in the view model.
            for (var i = 0; i < _diagnostics.Markers.Count; i++)
            {
                var topRect = (Rectangle)_markersTop.Children[i];
                var bottomRect = (Rectangle)_markersBottom.Children[i];
                var offset = _diagnostics.Markers[i].ConstrainedProgress;
                topRect.Margin = new Thickness((offset * barWidth) + c_trackMargin, 0, 0, 0);
                bottomRect.Margin = new Thickness((offset * barWidth) + c_trackMargin, 0, 0, 0);
            }

            return result;
        }

        // Returns a color brush which has its color bound to the property with the given name.
        static CompositionColorBrush CreateBoundColorBrush(CompositionPropertySet propertySet, string propertyName)
        {
            var c = propertySet.Compositor;
            var result = c.CreateColorBrush();
            propertySet.InsertColor(propertyName, default);
            var expressionAnimation = c.CreateExpressionAnimation($"our.{propertyName}");
            expressionAnimation.SetReferenceParameter("our", propertySet);
            result.StartAnimation(nameof(result.Color), expressionAnimation);
            return result;
        }

        Brush GetResourceBrush(string resourceName) => (Brush)App.Current.Resources[resourceName];

        Color GetResourceBrushColor(string resourceName) => ((SolidColorBrush)GetResourceBrush(resourceName)).Color;

        void UpdateColors()
        {
            switch (_currentVisualStateName)
            {
                case null:
                case "Normal":
                    SetColors(
                        markers: GetResourceBrushColor("LottieBasicBrush"),
                        thumbFill: GetResourceBrushColor("LottieBasicBrush"),
                        thumbStroke: Colors.Transparent,
                        track: GetResourceBrushColor("SliderTrackFill"),
                        decreaseRectangle: GetResourceBrushColor("LottieBasicBrush"));
                    break;
                case "Disabled":
                    SetColors(
                        markers: Colors.Transparent,
                        thumbFill: GetResourceBrushColor("DisabledBrush"),
                        thumbStroke: Colors.Transparent,
                        track: GetResourceBrushColor("SliderTrackFillDisabled"),
                        decreaseRectangle: GetResourceBrushColor("SliderTrackValueFillDisabled"));
                    break;
                case "Pressed":
                case "PointerOver":
                    SetColors(
                        markers: Colors.White,
                        thumbFill: GetResourceBrushColor("LottieBasicBrush"),
                        thumbStroke: GetResourceBrushColor("LottieBasicBrush"),
                        track: GetResourceBrushColor("SliderTrackFillPointerOver"),
                        decreaseRectangle: GetResourceBrushColor("LottieBasicBrush"));
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        void SetColors(Color markers, Color thumbFill, Color thumbStroke, Color track, Color decreaseRectangle)
        {
            _markerBrush.Color = markers;
            _properties.InsertColor("ThumbFillColor", thumbFill);
            _properties.InsertColor("ThumbStrokeColor", thumbStroke);
            _properties.InsertColor("TrackColor", track);
            _properties.InsertColor("DecreaseRectangleColor", decreaseRectangle);
        }

        // Called by our custom VisualStateManager when there is a transition to one of the CommonStates.
        internal void OnSliderVisualStateChange(string stateName)
        {
            _currentVisualStateName = stateName;
            UpdateColors();
        }

        // Returns a single-pixel-wide Rectangle for displaying a marker above or below the track.
        Rectangle CreateMarkerRectangle() => new Rectangle() { Fill = _markerBrush, Width = 1, HorizontalAlignment = HorizontalAlignment.Left };

        void Markers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // Add rectangles to display each marker. There are 2 rectangles - one
                    // that sits above the track, and one that sits below the track.
                    _markersTop.Children.Add(CreateMarkerRectangle());
                    _markersBottom.Children.Add(CreateMarkerRectangle());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    // One marker was removed - remove a rectangle from the top and bottom.
                    _markersTop.Children.RemoveAt(0);
                    _markersBottom.Children.RemoveAt(0);
                    break;

                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                    // Moving and replacing doesn't affect the number of items, so nothing to do.
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Remove all the rectangles.
                    _markersTop.Children.Clear();
                    _markersBottom.Children.Clear();
                    break;

                default:
                    throw new InvalidOperationException();
            }

            // Force another arrange so that the markers can be set to the correct positions.
            InvalidateArrange();
        }

        // Formats the tooltip text.
        sealed class ThumbTooltipConverter : IValueConverter
        {
            readonly Scrubber _owner;

            internal ThumbTooltipConverter(Scrubber owner)
            {
                _owner = owner;
            }

            object IValueConverter.Convert(object value, Type targetType, object parameter, string language)
            {
                var duration = _owner._diagnostics.LottieVisualDiagnostics.Duration;
                return $"    {_owner.Value:0.00}\r\n{_owner.Value * duration.TotalSeconds:0.00} secs";
            }

            object IValueConverter.ConvertBack(object value, Type targetType, object parameter, string language)
            {
                throw new NotImplementedException();
            }
        }
    }

    // A VisualStateManager for the Slider so we can track its visual states and update
    // the UI of the Scrubber in sync with the Slider.
    internal sealed class SliderVisualStateManager : VisualStateManager
    {
        // Keep track of the previous state so we don't notify the Scrubber of
        // the same state twice in succession.
        string _previousCommonState;

        protected override bool GoToStateCore(
            Control control,
            FrameworkElement templateRoot,
            string stateName,
            VisualStateGroup group,
            VisualState state,
            bool useTransitions)
        {
            // Find the Scrubber that this VisualStateManager is under.
            var scrubber = GetOwner(control);

            if (group?.Name == "CommonStates")
            {
                var newState = state?.Name;

                // Check whether we have already reported this state.
                if (_previousCommonState != newState)
                {
                    _previousCommonState = newState;
                    scrubber.OnSliderVisualStateChange(newState);
                }
            }

            // The base class does the work.
            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }

        // Returns the Scrubber that the given object is a descendant of.
        static Scrubber GetOwner(DependencyObject descendant)
        {
            var parent = VisualTreeHelper.GetParent(descendant);
            return parent is Scrubber scrubber ? scrubber : GetOwner(parent);
        }
    }

    public sealed class ScrubberValueChangedEventArgs
    {
        internal ScrubberValueChangedEventArgs(double oldValue, double newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public double NewValue { get; }

        public double OldValue { get; }
    }
}
