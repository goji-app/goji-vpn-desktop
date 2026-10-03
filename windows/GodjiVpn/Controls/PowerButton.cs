using GodjiVpn.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace GodjiVpn.Controls;

/// <summary>
/// Круглая кнопка подключения 80×80 по центру под глобусом — порт ConnectButton (Android
/// ConnectScreen.kt). Выключено — плотное стекло с иконкой питания; подключение — вокруг
/// вращается двухцветная дуга-спиннер (Teal + Terracotta, оборот за 1 с); включено — заливка
/// акцентным градиентом, мягкое свечение, подложка TealTint 96 и два расходящихся кольца
/// (scale 1→1.55, alpha .5→0, 2.4 с, сдвиг 1.2 с). Кольца и спиннер выходят за пределы 80×80,
/// не занимая места в раскладке.
/// </summary>
public class PowerButton : UserControl
{
    private readonly Grid _root = new() { Width = 80, Height = 80, ClipToBounds = false };
    private readonly Ellipse _glow = new() { Width = 96, Height = 96, IsHitTestVisible = false };
    private readonly Ellipse[] _rings = { new(), new() };
    private readonly Grid _spinnerGrid = new() { Width = 90, Height = 90, Margin = new Thickness(-5), IsHitTestVisible = false };
    private readonly RotateTransform _spinRotate = new(0, 45, 45);
    private readonly GlassPanel _glass = new();
    private readonly Ellipse _accent = new() { Width = 80, Height = 80 };
    private readonly Path _icon = new() { Width = 28, Height = 28, Stretch = Stretch.Uniform };
    private readonly DropShadowEffect _glowEffect = new() { BlurRadius = 22, ShadowDepth = 4, Direction = 270, Opacity = 1 };

    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(PowerButton), new PropertyMetadata(false, (d, _) => ((PowerButton)d).UpdateState()));

    public bool IsOn { get => (bool)GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy), typeof(bool), typeof(PowerButton), new PropertyMetadata(false, (d, _) => ((PowerButton)d).UpdateState()));

    public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(PowerButton));

    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public PowerButton()
    {
        Width = 80;
        Height = 80;
        Cursor = Cursors.Hand;
        HorizontalAlignment = HorizontalAlignment.Center;

        _glow.SetResourceReference(Shape.FillProperty, "TealTintBrush");
        _glow.HorizontalAlignment = HorizontalAlignment.Center;
        _glow.VerticalAlignment = VerticalAlignment.Center;
        _glow.Margin = new Thickness(-8);
        _root.Children.Add(_glow);

        foreach (var ring in _rings)
        {
            ring.Width = ring.Height = 80;
            ring.StrokeThickness = 2;
            ring.SetResourceReference(Shape.StrokeProperty, "TealBrush");
            ring.RenderTransformOrigin = new Point(0.5, 0.5);
            ring.RenderTransform = new ScaleTransform(1, 1);
            ring.IsHitTestVisible = false;
            ring.Opacity = 0;
            _root.Children.Add(ring);
        }

        // Дуга-спиннер: верхняя-левая четверть — Teal, верхняя-правая — Terracotta (как в эталоне).
        _spinnerGrid.RenderTransform = _spinRotate;
        _spinnerGrid.Children.Add(Arc(225, 90, "TealBrush"));
        _spinnerGrid.Children.Add(Arc(315, 90, "TerracottaBrush"));
        _root.Children.Add(_spinnerGrid);

        _glass.Style = (Style)Application.Current.FindResource("GlassStrong");
        _glass.CornerRadius = 999;
        _root.Children.Add(_glass);

        _accent.SetResourceReference(Shape.FillProperty, "AccentGradientBrush");
        _accent.Stroke = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
        _accent.StrokeThickness = 1;
        _accent.Effect = _glowEffect;
        _root.Children.Add(_accent);

        _icon.Data = (Geometry)Application.Current.FindResource("IconPower");
        _icon.IsHitTestVisible = false;
        _root.Children.Add(_icon);

        Content = _root;
        Press.SetScale(this, 0.94);
        MouseLeftButtonUp += (_, _) =>
        {
            if (Command?.CanExecute(null) == true) Command.Execute(null);
        };

        _spinRotate.CenterX = 45;
        _spinRotate.CenterY = 45;
        _root.Loaded += (_, _) => UpdateState();
    }

    private static Path Arc(double startDeg, double sweepDeg, string brushKey)
    {
        const double r = 45 - 1.25;
        const double c = 45;
        Point P(double deg) => new(c + r * Math.Cos(deg * Math.PI / 180), c + r * Math.Sin(deg * Math.PI / 180));
        var figure = new PathFigure { StartPoint = P(startDeg), IsClosed = false };
        figure.Segments.Add(new ArcSegment(P(startDeg + sweepDeg), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
        var path = new Path
        {
            Data = new PathGeometry(new[] { figure }),
            StrokeThickness = 2.5,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            Width = 90,
            Height = 90
        };
        path.SetResourceReference(Shape.StrokeProperty, brushKey);
        return path;
    }

    private void UpdateState()
    {
        var on = IsOn;
        var busy = IsBusy && !on;

        _glass.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        _accent.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _glow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (TryFindResource("AccentGlowColor") is Color glow) _glowEffect.Color = glow;
        if (on) _icon.Fill = Brushes.White;
        else _icon.SetResourceReference(Shape.FillProperty, "TextPrimaryBrush");

        _spinnerGrid.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever };
            _spinRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
        }
        else
        {
            _spinRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        for (var i = 0; i < _rings.Length; i++)
        {
            var ring = _rings[i];
            var scale = (ScaleTransform)ring.RenderTransform;
            if (on)
            {
                var begin = TimeSpan.FromMilliseconds(i * 1200);
                var duration = TimeSpan.FromMilliseconds(2400);
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                var grow = new DoubleAnimation(1, 1.55, duration) { BeginTime = begin, RepeatBehavior = Motion.Decor, EasingFunction = ease };
                var fade = new DoubleAnimation(0.5, 0, duration) { BeginTime = begin, RepeatBehavior = Motion.Decor, EasingFunction = ease };
                Timeline.SetDesiredFrameRate(grow, 30);
                Timeline.SetDesiredFrameRate(fade, 30);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                ring.BeginAnimation(OpacityProperty, fade);
            }
            else
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                ring.BeginAnimation(OpacityProperty, null);
                ring.Opacity = 0;
            }
        }
    }
}
