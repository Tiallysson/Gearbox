using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Gearbox.Modules.Notch.UI;

/// <summary>
/// Anel de uso (como um "radial chart" do shadcn): trilho translúcido e arco claro a partir do topo, no sentido
/// horário, com a porcentagem no meio. Acima de <see cref="WarningPercent"/>, o arco fica vermelho.
/// </summary>
public sealed partial class UsageRing : Grid
{
    public const double WarningPercent = 90;

    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(UsageRing), new PropertyMetadata(0.0, (d, _) => ((UsageRing)d).Update()));

    private const double RingSize = 52;
    private const double Thickness = 5;

    private readonly Ellipse _full;
    private readonly Path _arc;
    private readonly TextBlock _label;

    public UsageRing()
    {
        Width = Height = RingSize;
        var track = new Ellipse { Stroke = Brush("MutedBrush"), StrokeThickness = Thickness, Margin = new Thickness(Thickness / 2) };
        _full = new Ellipse { StrokeThickness = Thickness, Margin = new Thickness(Thickness / 2), Visibility = Visibility.Collapsed };
        _arc = new Path { StrokeThickness = Thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        _label = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
        };
        Children.Add(track);
        Children.Add(_full);
        Children.Add(_arc);
        Children.Add(_label);
        Update();
    }

    /// <summary>De 0 a 100.</summary>
    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    private void Update()
    {
        var geometry = RingGeometry.For(Percent, RingSize, Thickness);
        var brush = Percent >= WarningPercent ? Brush("DestructiveBrush") : Brush("PrimaryBrush");
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

    /// <summary>Pincéis do tema da ilha (<c>NotchTheme.xaml</c>). Só usado na thread de UI.</summary>
    private static Brush Brush(string key) => (Brush)s_theme.Value[key];

    private static readonly Lazy<NotchTheme> s_theme = new(() => new NotchTheme());
}
