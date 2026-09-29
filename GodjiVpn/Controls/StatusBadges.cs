using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace GodjiVpn.Controls;

/// <summary>
/// "Живой" бейдж статуса v5 ("АКТИВНА") — порт ActiveBadge (StatusBadges.kt): капсула с ядром
/// RingCore, по рамке которой бежит комета акцентного цвета, внутри — пульсирующая точка и
/// подпись. В WPF нет конического градиента, поэтому комета — вращающийся вокруг центра
/// градиентный сектор, обрезанный формой капсулы: сверху его закрывает ядро, видна только
/// полоска 1.5px по краю — визуально то же "кольцо-комета".
/// </summary>
public class ActiveBadge : UserControl
{
    private readonly TextBlock _label = new();
    private readonly Grid _outer = new();
    // Комета на Canvas: Canvas не участвует в размере бейджа — иначе её ширина (зависящая от
    // ширины бейджа) раздувала бы сам бейдж и раскладка уходила в бесконечный цикл.
    private readonly Canvas _cometLayer = new() { IsHitTestVisible = false };
    private readonly Border _core = new() { Margin = new Thickness(1.5), Padding = new Thickness(10, 0, 12, 0) };
    private readonly Rectangle _comet = new();
    private readonly RotateTransform _cometRotate = new();

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ActiveBadge),
        new PropertyMetadata("АКТИВНА", (d, e) => ((ActiveBadge)d)._label.Text = (string)e.NewValue));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public ActiveBadge()
    {
        Height = 28;
        _outer.ClipToBounds = true;
        _outer.SizeChanged += (_, _) => Layout();

        _comet.RenderTransform = _cometRotate;
        _comet.IsHitTestVisible = false;
        _cometLayer.Children.Add(_comet);
        _outer.Children.Add(_cometLayer);

        var core = _core;
        core.SetResourceReference(Border.BackgroundProperty, "RingCoreBrush");
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var dot = new Grid { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) };
        var ping = new Ellipse { RenderTransformOrigin = new Point(0.5, 0.5) };
        ping.SetResourceReference(Shape.FillProperty, "TealBrush");
        var pingScale = new ScaleTransform(1, 1);
        ping.RenderTransform = pingScale;
        var solid = new Ellipse();
        solid.SetResourceReference(Shape.FillProperty, "TealBrush");
        dot.Children.Add(ping);
        dot.Children.Add(solid);

        var pingDuration = new Duration(TimeSpan.FromMilliseconds(1600));
        var scaleAnim = new DoubleAnimation(1, 2.8, pingDuration) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var fadeAnim = new DoubleAnimation(0.75, 0, pingDuration) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        Timeline.SetDesiredFrameRate(scaleAnim, 30);
        Timeline.SetDesiredFrameRate(fadeAnim, 30);
        pingScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        pingScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        ping.BeginAnimation(OpacityProperty, fadeAnim);

        _label.Text = Text;
        _label.FontSize = 10;
        _label.FontWeight = FontWeights.ExtraBold;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.SetResourceReference(TextBlock.ForegroundProperty, "TealDeepBrush");
        _label.SetResourceReference(TextBlock.FontFamilyProperty, "ManropeFamily");

        row.Children.Add(dot);
        row.Children.Add(_label);
        core.Child = row;
        _outer.Children.Add(core);
        Content = _outer;

        var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(2800)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(spin, 30);
        _cometRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private void Layout()
    {
        var w = _outer.ActualWidth;
        var h = _outer.ActualHeight;
        if (w <= 0 || h <= 0) return;
        _outer.Clip = new RectangleGeometry(new Rect(0, 0, w, h), h / 2, h / 2);
        _core.CornerRadius = new CornerRadius((h - 3) / 2);

        var r = Math.Max(w, h);
        _comet.Width = r;
        _comet.Height = r;
        Canvas.SetLeft(_comet, w / 2);
        Canvas.SetTop(_comet, h / 2 - r);
        _cometRotate.CenterX = 0;
        _cometRotate.CenterY = r;
        var teal = TryFindResource("TealColor") is Color c ? c : Color.FromRgb(0, 167, 155);
        _comet.Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0, teal.R, teal.G, teal.B), 0),
            new GradientStop(teal, 0.55),
            new GradientStop(Colors.White, 0.8),
            new GradientStop(Color.FromArgb(0, teal.R, teal.G, teal.B), 1)
        }, new Point(0, 1), new Point(1, 0));
    }
}

/// <summary>Переливающийся "голографический" бейдж с бегущим бликом — порт HoloBadge
/// (v5 "Текущий" / "НОВАЯ").</summary>
public class HoloBadge : UserControl
{
    private static readonly Color[] Holo =
    {
        Color.FromRgb(0x1F, 0xC2, 0xB2), Color.FromRgb(0x7C, 0x8C, 0xFF), Color.FromRgb(0xE5, 0x8F, 0xD0),
        Color.FromRgb(0xF4, 0xC2, 0x7A), Color.FromRgb(0x1F, 0xC2, 0xB2)
    };

    private readonly TextBlock _icon = new();
    private readonly TextBlock _label = new();
    private readonly Border _body = new();
    private readonly Rectangle _shine = new();
    private readonly TranslateTransform _shineX = new();
    private readonly TranslateTransform _holoX = new();

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(HoloBadge),
        new PropertyMetadata("Текущий", (d, e) => ((HoloBadge)d)._label.Text = (string)e.NewValue));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(HoloBadge),
        new PropertyMetadata("★", (d, e) => ((HoloBadge)d)._icon.Text = (string)e.NewValue));

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public static readonly DependencyProperty ReverseProperty = DependencyProperty.Register(
        nameof(Reverse), typeof(bool), typeof(HoloBadge),
        new PropertyMetadata(false, (d, _) => ((HoloBadge)d).UpdateBrush()));

    public bool Reverse { get => (bool)GetValue(ReverseProperty); set => SetValue(ReverseProperty, value); }

    public HoloBadge()
    {
        Height = 22;
        VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ClipToBounds = true };
        _body.CornerRadius = new CornerRadius(11);
        _body.Padding = new Thickness(8, 0, 8, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _icon.Text = Icon;
        _icon.FontSize = 9;
        _icon.Foreground = Brushes.White;
        _icon.Margin = new Thickness(0, 0, 4, 0);
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _label.Text = Text;
        _label.FontSize = 9.5;
        _label.FontWeight = FontWeights.ExtraBold;
        _label.Foreground = Brushes.White;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.SetResourceReference(TextBlock.FontFamilyProperty, "ManropeFamily");
        row.Children.Add(_icon);
        row.Children.Add(_label);
        _body.Child = row;
        grid.Children.Add(_body);

        _shine.Width = 20;
        _shine.HorizontalAlignment = HorizontalAlignment.Left;
        _shine.IsHitTestVisible = false;
        _shine.RenderTransform = _shineX;
        _shine.Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(178, 255, 255, 255), 0.5),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
        }, new Point(0, 0), new Point(1, 0));
        grid.Children.Add(_shine);
        Content = grid;

        grid.SizeChanged += (_, _) =>
        {
            grid.Clip = new RectangleGeometry(new Rect(0, 0, grid.ActualWidth, grid.ActualHeight), 11, 11);
            UpdateBrush();
            var w = grid.ActualWidth;
            var sweep = new DoubleAnimation(-0.4 * w - 10, 1.6 * w - 10, TimeSpan.FromMilliseconds(3000))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Timeline.SetDesiredFrameRate(sweep, 30);
            _shineX.BeginAnimation(TranslateTransform.XProperty, sweep);
        };
        UpdateBrush();
    }

    private void UpdateBrush()
    {
        var colors = Reverse ? Holo.Reverse().ToArray() : Holo;
        var stops = new GradientStopCollection();
        for (var i = 0; i < colors.Length; i++) stops.Add(new GradientStop(colors[i], (double)i / (colors.Length - 1)));
        var width = Math.Max(1, ActualWidth) * 3;
        var brush = new LinearGradientBrush(stops, new Point(0, 0), new Point(width, 0))
        {
            MappingMode = BrushMappingMode.Absolute,
            SpreadMethod = GradientSpreadMethod.Repeat,
            Transform = _holoX
        };
        _body.Background = brush;
        var shift = new DoubleAnimation(0, -width, TimeSpan.FromMilliseconds(5000)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(shift, 30);
        _holoX.BeginAnimation(TranslateTransform.XProperty, shift);
    }
}
