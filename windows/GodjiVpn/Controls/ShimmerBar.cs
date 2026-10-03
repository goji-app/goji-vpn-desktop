using GodjiVpn.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace GodjiVpn.Controls;

/// <summary>Полоса трафика v5 — порт AnimatedTrafficBar (ConnectScreen.kt): дорожка TrackBg 8,
/// заливка акцентным градиентом, плавно (0.9 с) догоняющая новое значение, и бегущий по
/// заливке блик шириной 40% заливки (от −100% до 300% за 1.8 с).</summary>
public class ShimmerBar : UserControl
{
    private readonly Grid _track = new() { ClipToBounds = true };
    private readonly Border _fill = new() { HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true };
    private readonly Rectangle _shine = new() { HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    private readonly TranslateTransform _shineX = new();

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ShimmerBar), new PropertyMetadata(0.0, (d, _) => ((ShimmerBar)d).Update(animate: true)));

    /// <summary>Доля 0..1.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public ShimmerBar()
    {
        Height = 8;
        _track.SetResourceReference(Panel.BackgroundProperty, "TrackBgBrush");
        _fill.SetResourceReference(Border.BackgroundProperty, "AccentGradientBrush");
        _fill.CornerRadius = new CornerRadius(4);
        _fill.Width = 0;
        _shine.RenderTransform = _shineX;
        _shine.Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(140, 255, 255, 255), 0.5),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
        }, new Point(0, 0), new Point(1, 0));
        var fillGrid = new Grid();
        fillGrid.Children.Add(_shine);
        _fill.Child = fillGrid;
        _track.Children.Add(_fill);
        Content = _track;
        SizeChanged += (_, _) =>
        {
            _track.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), ActualHeight / 2, ActualHeight / 2);
            Update(animate: false);
        };
    }

    private void Update(bool animate)
    {
        var width = ActualWidth;
        if (width <= 0) return;
        var target = width * Math.Clamp(Value, 0, 1);
        if (animate)
        {
            _fill.BeginAnimation(WidthProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(900))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });
        }
        else
        {
            _fill.BeginAnimation(WidthProperty, null);
            _fill.Width = target;
        }

        var shine = Math.Max(4, target * 0.4);
        _shine.Width = shine;
        var anim = new DoubleAnimation(-shine, shine * 3, TimeSpan.FromMilliseconds(1800)) { RepeatBehavior = Motion.Decor };
        Timeline.SetDesiredFrameRate(anim, 30);
        _shineX.BeginAnimation(TranslateTransform.XProperty, target > 0 ? anim : null);
    }
}
