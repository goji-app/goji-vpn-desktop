using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GodjiVpn.Services;

namespace GodjiVpn.Controls;

/// <summary>
/// Общий фон дизайна v5 — порт GlassBackdrop Windows-клиента: вертикальный градиент, тонкие
/// концентрические кольца, сетка точек и четыре цветных пятна (при подключённом VPN первое
/// пятно ярче). В эталоне пятна медленно плавают, здесь стоят в середине траектории: фон во
/// всё окно, и его перерисовка 20 раз в секунду (вместе со всем, что над ним) держала процессор
/// занятым в простое. Перерисовывается только при смене темы, размера и статуса VPN.
/// </summary>
public class GlassBackdrop : Control
{
    private sealed record BlobSpec(double Cx, double Cy, double R, double Dx, double Dy, double S0, double S1);

    private static readonly BlobSpec[] Blobs =
    {
        new(56, 109, 180, 60, 40, 1, 1.18),
        new(345, 249, 160, -50, 70, 1.1, 0.9),
        new(129, 634, 170, 40, -60, 0.95, 1.15),
        new(356, 757, 150, 60, 40, 1, 1.18),
    };

    public GlassBackdrop()
    {
        IsHitTestVisible = false;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (VpnEngine.Current != null) VpnEngine.Current.PropertyChanged += OnVpnChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (VpnEngine.Current != null) VpnEngine.Current.PropertyChanged -= OnVpnChanged;
    }

    private void OnVpnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VpnEngine.IsRunning))
            Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
    }

    private Color C(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is Color c ? c : Colors.Transparent;

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        ctx.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(C("BaseTopColor"), 0),
                new GradientStop(C("BaseMidColor"), 0.55),
                new GradientStop(C("BaseBottomColor"), 1)
            }
        }, null, new Rect(0, 0, w, h));

        var alpha = this.TryFindResource("BlobAlpha", ActualThemeVariant, out var a) && a is double d ? d : 0.6;
        var blob1Boost = VpnEngine.Current?.IsRunning == true ? 0.15 : 0;

        var kx = w / 412.0;
        var ky = h / 892.0;
        const double feather = 26;
        for (var i = 0; i < Blobs.Length; i++)
        {
            var b = Blobs[i];
            var color = C($"Blob{i + 1}Color");
            const double t = 0.5;
            var scale = b.S0 + (b.S1 - b.S0) * t;
            var radius = b.R * kx * scale;
            var outer = radius + feather;
            var core = Math.Clamp((radius - feather) / outer, 0, 1);
            var center = new Point((b.Cx + b.Dx * t) * kx, (b.Cy + b.Dy * t) * ky);
            var brush = new RadialGradientBrush
            {
                Opacity = Math.Min(1, alpha + (i == 0 ? blob1Boost : 0)),
                GradientStops =
                {
                    new GradientStop(color, 0),
                    new GradientStop(color, core),
                    new GradientStop(Color.FromArgb(128, color.R, color.G, color.B), radius / outer),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                }
            };
            ctx.DrawEllipse(brush, null, center, outer, outer);
        }

        var ringPen = new Pen(new SolidColorBrush(C("TexLineColor")), 1);
        var ringCenter = new Point(w * 0.78, h * 0.18);
        var maxR = Math.Sqrt(w * w + h * h);
        for (double r = 27; r < maxR; r += 27) ctx.DrawEllipse(null, ringPen, ringCenter, r, r);

        var dot = new SolidColorBrush(C("TexDotColor"));
        for (double y = 8; y < h; y += 16)
            for (double x = 8; x < w; x += 16)
                ctx.DrawRectangle(dot, null, new Rect(x - 1, y - 1, 2, 2), 1, 1);
    }
}
