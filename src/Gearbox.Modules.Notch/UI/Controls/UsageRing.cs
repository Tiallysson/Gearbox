using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Gearbox.Modules.Notch.UI.Controls;

public sealed partial class UsageRing : Grid
{
    public const double WarningPercent = 90;

    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(UsageRing), new PropertyMetadata(0.0, (d, _) => ((UsageRing)d).Update()));

    public static readonly DependencyProperty TrackBrushProperty = BrushProperty(nameof(TrackBrush));

    public static readonly DependencyProperty ValueBrushProperty = BrushProperty(nameof(ValueBrush));

    public static readonly DependencyProperty WarningBrushProperty = BrushProperty(nameof(WarningBrush));

    private const double RingSize = 52;
    private const double Thickness = 5;

    private readonly Ellipse _track;
    private readonly Ellipse _full;
    private readonly Path _arc;
    private readonly TextBlock _label;

    public UsageRing()
    {
        Width = Height = RingSize;
        _track = new Ellipse { StrokeThickness = Thickness, Margin = new Thickness(Thickness / 2) };
        _full = new Ellipse { StrokeThickness = Thickness, Margin = new Thickness(Thickness / 2), Visibility = Visibility.Collapsed };
        _arc = new Path { StrokeThickness = Thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        _label = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
        };
        Children.Add(_track);
        Children.Add(_full);
        Children.Add(_arc);
        Children.Add(_label);
        Update();
    }

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush? ValueBrush
    {
        get => (Brush?)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public Brush? WarningBrush
    {
        get => (Brush?)GetValue(WarningBrushProperty);
        set => SetValue(WarningBrushProperty, value);
    }

    private void Update()
    {
        var geometry = RingGeometry.For(Percent, RingSize, Thickness);
        _track.Stroke = TrackBrush;
        var brush = Percent >= WarningPercent ? WarningBrush : ValueBrush;
        _arc.Stroke = _full.Stroke = brush;
        _full.Visibility = geometry.IsFull ? Visibility.Visible : Visibility.Collapsed;
        _arc.Visibility = geometry.IsFull || geometry.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        _label.Text = ModuleStrings.Format("PercentFormat", (int)Math.Round(Percent));

        if (!geometry.IsFull && !geometry.IsEmpty)
        {
            var figure = new PathFigure { StartPoint = new Point(geometry.StartX, geometry.StartY), IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(geometry.EndX, geometry.EndY),
                Size = new Size(geometry.Radius, geometry.Radius),
                IsLargeArc = geometry.IsLargeArc,
                SweepDirection = SweepDirection.Clockwise,
            });
            var path = new PathGeometry();
            path.Figures.Add(figure);
            _arc.Data = path;
        }
    }

    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(
        name, typeof(Brush), typeof(UsageRing), new PropertyMetadata(null, (d, _) => ((UsageRing)d).Update()));
}
