using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using GodjiVpn.Services;

namespace GodjiVpn.Controls;

/// <summary>
/// Общий фон приложения v5 — рисуется ОДИН раз под всем содержимым окна (MainWindow,
/// SupportWindow). Порт Android GlassBackdrop.kt, слои снизу вверх, 1:1 с эталоном:
///  1. Вертикальный градиент BaseTop → BaseMid (55%) → BaseBottom.
///  2. Концентрические кольца: центр (78% w, 18% h), шаг 27, линия 1, TexLine.
///  3. Сетка точек: шаг 16, радиус 1, TexDot.
///  4. Четыре больших размытых пятна Blob1..4 (альфа BlobAlpha). В эталоне они медленно
///     дрейфуют (14–19 с), но здесь стоят на месте: пятна во весь экран, и любое их движение
///     заставляло WPF перерисовывать всё окно со всеми стеклянными карточками 30 раз в секунду
///     (заметная нагрузка на процессор в простое). Ставим их в середину траектории дрейфа.
/// Координаты пятен заданы для экрана 412×892 и масштабируются под реальный размер.
/// Без этого фона стеклянные карточки выглядят просто серыми плашками — "стекло" получается
/// именно слоями поверх мягкого пёстрого фона.
/// </summary>
public class GlassBackdrop : Grid
{
    private sealed record BlobSpec(double Cx, double Cy, double R, double Dx, double Dy, double S0, double S1);

    // cx/cy/r — для макета 412×892; dx/dy — амплитуда дрейфа эталона; s0→s1 — масштаб.
    private static readonly BlobSpec[] Blobs =
    {
        new(56, 109, 180, 60, 40, 1, 1.18),
        new(345, 249, 160, -50, 70, 1.1, 0.9),
        new(129, 634, 170, 40, -60, 0.95, 1.15),
        new(356, 757, 150, 60, 40, 1, 1.18),
    };

    private readonly Texture _texture = new();
    private readonly Canvas _blobLayer = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly Ellipse[] _blobs = new Ellipse[Blobs.Length];
    private bool _connected;

    public GlassBackdrop()
    {
        IsHitTestVisible = false;
        Children.Add(_texture);
        Children.Add(_blobLayer);
        for (var i = 0; i < Blobs.Length; i++)
        {
            _blobs[i] = new Ellipse { IsHitTestVisible = false };
            _blobLayer.Children.Add(_blobs[i]);
        }
        SizeChanged += (_, _) => RebuildBlobs();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ThemeService.Current != null) ThemeService.Current.Changed += OnThemeChanged;
        if (VpnEngine.Current != null)
        {
            VpnEngine.Current.PropertyChanged += OnVpnChanged;
            _connected = VpnEngine.Current.IsRunning;
        }
        RebuildBlobs();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ThemeService.Current != null) ThemeService.Current.Changed -= OnThemeChanged;
        if (VpnEngine.Current != null) VpnEngine.Current.PropertyChanged -= OnVpnChanged;
    }

    private void OnThemeChanged()
    {
        _texture.InvalidateVisual();
        RebuildBlobs();
    }

    /// <summary>При подключении первое (тиловое) пятно становится ярче — фон "оживает".</summary>
    private void OnVpnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VpnEngine.IsRunning)) return;
        Dispatcher.BeginInvoke(() =>
        {
            var connected = VpnEngine.Current?.IsRunning == true;
            if (connected == _connected) return;
            _connected = connected;
            ApplyBlobOpacity(animate: true);
        });
    }

    private double BlobAlpha => TryFindResource("BlobAlpha") is double a ? a : 0.6;

    private void ApplyBlobOpacity(bool animate)
    {
        var alpha = BlobAlpha;
        for (var i = 0; i < _blobs.Length; i++)
        {
            var target = Math.Min(1, i == 0 && _connected ? alpha + 0.15 : alpha);
            if (animate)
                _blobs[i].BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(600)));
            else
            {
                _blobs[i].BeginAnimation(OpacityProperty, null);
                _blobs[i].Opacity = target;
            }
        }
    }

    private void RebuildBlobs()
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var kx = w / 412.0;
        var ky = h / 892.0;
        const double feather = 26;

        for (var i = 0; i < Blobs.Length; i++)
        {
            var b = Blobs[i];
            var color = TryFindResource($"Blob{i + 1}Color") is Color c ? c : Colors.Transparent;
            var radius = b.R * kx;
            var outer = radius + feather;
            var core = Math.Clamp((radius - feather) / outer, 0, 1);
            // Эквивалент CSS "круг + filter: blur(26px)": сплошное ядро и мягкий край.
            var brush = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(color, 0),
                    new GradientStop(color, core),
                    new GradientStop(Color.FromArgb(128, color.R, color.G, color.B), radius / outer),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                }
            };
            brush.Freeze();

            var e = _blobs[i];
            e.Fill = brush;
            e.Width = e.Height = outer * 2;
            Canvas.SetLeft(e, -outer);
            Canvas.SetTop(e, -outer);

            var s = (b.S0 + b.S1) / 2;
            var transform = new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(s, s),
                    new TranslateTransform((b.Cx + b.Dx / 2) * kx, (b.Cy + b.Dy / 2) * ky)
                }
            };
            transform.Freeze();
            e.RenderTransform = transform;
        }
        ApplyBlobOpacity(animate: false);
    }

    /// <summary>Статичная часть фона: градиент, кольца, точки — перерисовывается только при
    /// смене размера/темы.</summary>
    private sealed class Texture : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            Color C(string key) => TryFindResource(key) is Color c ? c : Colors.Transparent;

            var baseBrush = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(C("BaseTopColor"), 0),
                new GradientStop(C("BaseMidColor"), 0.55),
                new GradientStop(C("BaseBottomColor"), 1)
            }, new Point(0, 0), new Point(0, 1));
            baseBrush.Freeze();
            dc.DrawRectangle(baseBrush, null, new Rect(0, 0, w, h));

            var ringPen = new Pen(new SolidColorBrush(C("TexLineColor")), 1);
            ringPen.Freeze();
            var center = new Point(w * 0.78, h * 0.18);
            var maxR = Math.Sqrt(w * w + h * h);
            for (double r = 27; r < maxR; r += 27) dc.DrawEllipse(null, ringPen, center, r, r);

            var dotBrush = new SolidColorBrush(C("TexDotColor"));
            dotBrush.Freeze();
            var tile = new DrawingBrush(new GeometryDrawing(dotBrush, null, new EllipseGeometry(new Point(8, 8), 1, 1)))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 16, 16),
                ViewboxUnits = BrushMappingMode.Absolute
            };
            tile.Freeze();
            dc.DrawRectangle(tile, null, new Rect(0, 0, w, h));
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }
    }
}
