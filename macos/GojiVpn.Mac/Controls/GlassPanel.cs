using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace GodjiVpn.Controls;

public enum GlassLevel { Normal, Strong, Faint }

public enum GlassShadow { None, Card, Strong, Pill }

/// <summary>
/// Общие "часы" анимаций стекла и фона: один таймер на всё приложение вместо анимации в
/// каждом контроле. Подписчики перерисовываются по тику, пока находятся в визуальном дереве.
/// </summary>
internal static class GlassClock
{
    public static readonly Stopwatch Time = Stopwatch.StartNew();
    private static readonly HashSet<Control> Subscribers = new();
    private static DispatcherTimer? _timer;

    public static void Subscribe(Control c)
    {
        Subscribers.Add(c);
        if (_timer != null) return;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Render, (_, _) =>
        {
            foreach (var s in Subscribers) s.InvalidateVisual();
        });
        _timer.Start();
    }

    public static void Unsubscribe(Control c)
    {
        Subscribers.Remove(c);
        if (Subscribers.Count > 0 || _timer == null) return;
        _timer.Stop();
        _timer = null;
    }

    /// <summary>0→1→0 за 2·period секунд с плавным ускорением/замедлением (как CubicEase InOut).</summary>
    public static double PingPong(double periodSeconds, double offsetSeconds = 0)
    {
        var t = (Time.Elapsed.TotalSeconds + offsetSeconds) % (periodSeconds * 2) / periodSeconds;
        if (t > 1) t = 2 - t;
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }
}

/// <summary>
/// Стеклянная поверхность дизайна v5 — порт GlassPanel Windows-клиента (а тот — CardStyle.kt
/// Android). Снизу вверх внутри формы: тень → заливка по уровню → Tint → бегающий блик →
/// диагональный глянец → внутренние тени сверху и снизу → контент → градиентная кромка 1px
/// (+ акцентная обводка) отдельным слоем поверх контента. CornerRadius зажимается до половины
/// меньшей стороны — 999 даёт капсулу/круг. Цвета рецепта выставляет стиль из Themes/Glass.axaml.
/// </summary>
public class GlassPanel : Decorator, ICustomHitTest
{
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<GlassPanel, double>(nameof(CornerRadius), 26);
    public static readonly StyledProperty<GlassLevel> LevelProperty =
        AvaloniaProperty.Register<GlassPanel, GlassLevel>(nameof(Level));
    public static readonly StyledProperty<GlassShadow> ShadowProperty =
        AvaloniaProperty.Register<GlassPanel, GlassShadow>(nameof(Shadow), GlassShadow.Card);
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<GlassPanel, IBrush?>(nameof(Tint));
    public static readonly StyledProperty<bool> AccentBorderProperty =
        AvaloniaProperty.Register<GlassPanel, bool>(nameof(AccentBorder));
    public static readonly StyledProperty<bool> ClipContentProperty =
        AvaloniaProperty.Register<GlassPanel, bool>(nameof(ClipContent), true);

    public static readonly StyledProperty<Color> GlassColorProperty = ColorProp(nameof(GlassColor));
    public static readonly StyledProperty<Color> GlassStrongColorProperty = ColorProp(nameof(GlassStrongColor));
    public static readonly StyledProperty<Color> GlassFaintColorProperty = ColorProp(nameof(GlassFaintColor));
    public static readonly StyledProperty<Color> SpotColorProperty = ColorProp(nameof(SpotColor));
    public static readonly StyledProperty<Color> GlossColorProperty = ColorProp(nameof(GlossColor));
    public static readonly StyledProperty<Color> RimAColorProperty = ColorProp(nameof(RimAColor));
    public static readonly StyledProperty<Color> RimBColorProperty = ColorProp(nameof(RimBColor));
    public static readonly StyledProperty<Color> RimCColorProperty = ColorProp(nameof(RimCColor));
    public static readonly StyledProperty<Color> RimDColorProperty = ColorProp(nameof(RimDColor));
    public static readonly StyledProperty<Color> InnerTopColorProperty = ColorProp(nameof(InnerTopColor));
    public static readonly StyledProperty<Color> InnerBottomColorProperty = ColorProp(nameof(InnerBottomColor));
    public static readonly StyledProperty<Color> ShadowColorProperty = ColorProp(nameof(ShadowColor));
    public static readonly StyledProperty<Color> AccentColorProperty = ColorProp(nameof(AccentColor));

    private static StyledProperty<Color> ColorProp(string name) =>
        AvaloniaProperty.Register<GlassPanel, Color>(name, Colors.Transparent);

    public double CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public GlassLevel Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public GlassShadow Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }
    public IBrush? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public bool AccentBorder { get => GetValue(AccentBorderProperty); set => SetValue(AccentBorderProperty, value); }
    public bool ClipContent { get => GetValue(ClipContentProperty); set => SetValue(ClipContentProperty, value); }
    public Color GlassColor { get => GetValue(GlassColorProperty); set => SetValue(GlassColorProperty, value); }
    public Color GlassStrongColor { get => GetValue(GlassStrongColorProperty); set => SetValue(GlassStrongColorProperty, value); }
    public Color GlassFaintColor { get => GetValue(GlassFaintColorProperty); set => SetValue(GlassFaintColorProperty, value); }
    public Color SpotColor { get => GetValue(SpotColorProperty); set => SetValue(SpotColorProperty, value); }
    public Color GlossColor { get => GetValue(GlossColorProperty); set => SetValue(GlossColorProperty, value); }
    public Color RimAColor { get => GetValue(RimAColorProperty); set => SetValue(RimAColorProperty, value); }
    public Color RimBColor { get => GetValue(RimBColorProperty); set => SetValue(RimBColorProperty, value); }
    public Color RimCColor { get => GetValue(RimCColorProperty); set => SetValue(RimCColorProperty, value); }
    public Color RimDColor { get => GetValue(RimDColorProperty); set => SetValue(RimDColorProperty, value); }
    public Color InnerTopColor { get => GetValue(InnerTopColorProperty); set => SetValue(InnerTopColorProperty, value); }
    public Color InnerBottomColor { get => GetValue(InnerBottomColorProperty); set => SetValue(InnerBottomColorProperty, value); }
    public Color ShadowColor { get => GetValue(ShadowColorProperty); set => SetValue(ShadowColorProperty, value); }
    public Color AccentColor { get => GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }

    private readonly RimLayer _rim;

    static GlassPanel()
    {
        AffectsRender<GlassPanel>(CornerRadiusProperty, LevelProperty, ShadowProperty, TintProperty, AccentBorderProperty,
            GlassColorProperty, GlassStrongColorProperty, GlassFaintColorProperty, SpotColorProperty, GlossColorProperty,
            RimAColorProperty, RimBColorProperty, RimCColorProperty, RimDColorProperty, InnerTopColorProperty,
            InnerBottomColorProperty, ShadowColorProperty, AccentColorProperty);
        AffectsArrange<GlassPanel>(CornerRadiusProperty, ClipContentProperty);
    }

    public GlassPanel()
    {
        _rim = new RimLayer(this) { IsHitTestVisible = false, ZIndex = int.MaxValue };
        VisualChildren.Add(_rim);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBorderProperty || change.Property.Name.StartsWith("Rim") ||
            change.Property == CornerRadiusProperty || change.Property == AccentColorProperty)
            _rim.InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        GlassClock.Subscribe(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        GlassClock.Unsubscribe(this);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _rim.Measure(availableSize);
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _rim.Arrange(new Rect(size));
        if (Child != null)
        {
            if (ClipContent)
            {
                // Clip — в координатах ребёнка, сдвинутых на Padding/Margin/выравнивание.
                var r = EffectiveRadius(size);
                var off = Child.Bounds.Position;
                Child.Clip = new RectangleGeometry(new Rect(-off.X, -off.Y, size.Width, size.Height), r, r);
            }
            else
            {
                Child.Clip = null;
            }
        }
        return size;
    }

    internal double EffectiveRadius(Size size) =>
        Math.Max(0, Math.Min(CornerRadius, Math.Min(size.Width, size.Height) / 2));

    public bool HitTest(Point point)
    {
        var size = Bounds.Size;
        if (point.X < 0 || point.Y < 0 || point.X > size.Width || point.Y > size.Height) return false;
        var r = EffectiveRadius(size);
        var cx = Math.Clamp(point.X, r, size.Width - r);
        var cy = Math.Clamp(point.Y, r, size.Height - r);
        var dx = point.X - cx;
        var dy = point.Y - cy;
        return dx * dx + dy * dy <= r * r;
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        var rect = new Rect(size);
        var r = EffectiveRadius(size);
        var shape = new RoundedRect(rect, r);

        var fill = Level switch
        {
            GlassLevel.Strong => GlassStrongColor,
            GlassLevel.Faint => GlassFaintColor,
            _ => GlassColor
        };
        var (blur, dy) = Shadow switch
        {
            GlassShadow.Card => (22.0, 10.0),
            GlassShadow.Strong => (26.0, 12.0),
            GlassShadow.Pill => (12.0, 5.0),
            _ => (0.0, 0.0)
        };
        // Тень рисуется размытием самой Avalonia; заливка стекла полупрозрачна, поэтому тень
        // рисуем отдельной непрозрачной формой, обрезанной снаружи, — иначе она просвечивала бы.
        if (blur > 0 && ShadowColor.A > 0)
        {
            var shadowGeometry = new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(rect.Inflate(blur * 2 + dy)), new RectangleGeometry(rect, r, r));
            using (ctx.PushGeometryClip(shadowGeometry))
            {
                ctx.DrawRectangle(Brushes.Black, null, shape, new BoxShadows(new BoxShadow
                {
                    OffsetY = dy,
                    Blur = blur,
                    Color = ShadowColor
                }));
            }
        }

        ctx.DrawRectangle(new SolidColorBrush(fill), null, shape);
        if (Tint != null) ctx.DrawRectangle(Tint, null, shape);

        using (ctx.PushClip(shape))
        {
            // Бегающий блик: x .15→.85, y .05→.30 ширины/высоты за 7 с туда-обратно.
            var p = GlassClock.PingPong(7);
            var center = new Point(size.Width * (0.15 + 0.7 * p), size.Height * (0.05 + 0.25 * p));
            var spot = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(SpotColor, 0),
                    new GradientStop(WithAlpha(SpotColor, 0), 1)
                }
            };
            ctx.DrawEllipse(spot, null, center, 240, 240);

            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(GlossColor, 0), new GradientStop(WithAlpha(GlossColor, 0), 0.45) }
            }, null, rect);

            const double innerH = 14;
            var innerTop = WithAlpha(InnerTopColor, (byte)(InnerTopColor.A * 0.45));
            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(innerTop, 0), new GradientStop(WithAlpha(innerTop, 0), 1) }
            }, null, new Rect(0, 0, size.Width, Math.Min(innerH, size.Height)));
            var bottomH = Math.Min(innerH * 1.3, size.Height);
            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(WithAlpha(InnerBottomColor, 0), 0), new GradientStop(InnerBottomColor, 1) }
            }, null, new Rect(0, size.Height - bottomH, size.Width, bottomH));
        }
    }

    internal static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>Кромка и акцентная обводка — поверх контента (иначе подсветка строк до краёв
    /// её перекрывала бы).</summary>
    private sealed class RimLayer : Control
    {
        private readonly GlassPanel _owner;

        public RimLayer(GlassPanel owner) => _owner = owner;

        public override void Render(DrawingContext ctx)
        {
            var size = Bounds.Size;
            if (size.Width <= 1 || size.Height <= 1) return;
            var r = _owner.EffectiveRadius(size);
            var rim = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(size.Width * 0.82, size.Height, RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(_owner.RimAColor, 0),
                    new GradientStop(_owner.RimBColor, 0.35),
                    new GradientStop(_owner.RimCColor, 0.62),
                    new GradientStop(_owner.RimDColor, 1)
                }
            };
            var edge = new Rect(0.5, 0.5, size.Width - 1, size.Height - 1);
            ctx.DrawRectangle(null, new Pen(rim, 1), new RoundedRect(edge, Math.Max(0, r - 0.5)));
            if (_owner.AccentBorder)
            {
                var ar = new Rect(0.75, 0.75, size.Width - 1.5, size.Height - 1.5);
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(_owner.AccentColor), 1.5), new RoundedRect(ar, Math.Max(0, r - 0.75)));
            }
        }
    }
}
