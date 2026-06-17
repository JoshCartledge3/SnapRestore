using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SnapRestore.Controls;

public sealed class CircularProgressRing : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CircularProgressRing, double>(nameof(Value));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgressRing, double>(nameof(StrokeThickness), 9);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressRing, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<CircularProgressRing, IBrush?>(nameof(ProgressBrush));

    static CircularProgressRing()
    {
        AffectsRender<CircularProgressRing>(
            ValueProperty,
            StrokeThicknessProperty,
            TrackBrushProperty,
            ProgressBrushProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var strokeThickness = Math.Max(0, StrokeThickness);
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - strokeThickness / 2);
        if (radius <= 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var trackPen = new Pen(TrackBrush, strokeThickness);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        var progress = Math.Clamp(Value, 0, 100) / 100;
        if (progress <= 0)
        {
            return;
        }

        if (progress >= 1)
        {
            var progressPen = new Pen(ProgressBrush, strokeThickness);
            context.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        var startAngle = -90d;
        var endAngle = startAngle + progress * 360d;
        var startPoint = PointOnCircle(center, radius, startAngle);
        var endPoint = PointOnCircle(center, radius, endAngle);

        var geometry = new StreamGeometry();
        using (var contextGeometry = geometry.Open())
        {
            contextGeometry.BeginFigure(startPoint, false);
            contextGeometry.ArcTo(
                endPoint,
                new Size(radius, radius),
                0,
                progress > 0.5,
                SweepDirection.Clockwise);
            contextGeometry.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(ProgressBrush, strokeThickness), geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var angle = Math.PI * angleDegrees / 180d;
        return new Point(
            center.X + radius * Math.Cos(angle),
            center.Y + radius * Math.Sin(angle));
    }
}
