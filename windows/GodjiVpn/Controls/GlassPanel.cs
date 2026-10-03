using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GodjiVpn.Controls;

public enum GlassLevel { Normal, Strong, Faint }

public enum GlassShadow { None, Card, Strong, Pill }

/// <summary>
/// Стеклянная поверхность дизайна v5 — порт Android CardStyle.kt (godjiCard/godjiGlassStrong/
/// godjiGlassPill/godjiGlassFlat) без эффектов и библиотек: "стекло" получается слоями поверх
/// мягкого общего фона (GlassBackdrop), как и в эталоне. Снизу вверх внутри формы: заливка по
/// уровню → цветной Tint (если задан) → бегающий радиальный блик → диагональный глянец →
/// светлая внутренняя тень сверху и тёмная снизу → контент → градиентная кромка 1px (+ акцентная
/// обводка 1.5px). Снаружи — мягкая тень только ЗА пределами формы.
///
/// CornerRadius зажимается до половины меньшей стороны — CornerRadius="999" даёт капсулу/круг,
/// без искажений, которые давал бы обычный Border с радиусом больше половины высоты.
/// Цвета рецепта — DP, выставленные неявным стилем из App.xaml через DynamicResource, поэтому
/// смена темы перерисовывает все панели сама, без ручной подписки.
/// </summary>
public class GlassPanel : Decorator
{
    private readonly DrawingVisual _overlay = new();

    public GlassPanel()
    {
        AddVisualChild(_overlay);
        SnapsToDevicePixels = false;
    }

    #region layout / shape properties

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(GlassPanel),
        new FrameworkPropertyMetadata(26.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Thickness), typeof(GlassPanel),
        new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(GlassLevel), typeof(GlassPanel),
        new FrameworkPropertyMetadata(GlassLevel.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public GlassLevel Level { get => (GlassLevel)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    public static readonly DependencyProperty ShadowProperty = DependencyProperty.Register(
        nameof(Shadow), typeof(GlassShadow), typeof(GlassPanel),
        new FrameworkPropertyMetadata(GlassShadow.Card, FrameworkPropertyMetadataOptions.AffectsRender));

    public GlassShadow Shadow { get => (GlassShadow)GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>Цвет поверх стекла (TealTint, TerracottaTint, баннеры…). null — чистое стекло.</summary>
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Brush), typeof(GlassPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Tint { get => (Brush?)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    /// <summary>Акцентная обводка 1.5px (выбранный сервер, текущий тариф).</summary>
    public static readonly DependencyProperty AccentBorderProperty = DependencyProperty.Register(
        nameof(AccentBorder), typeof(bool), typeof(GlassPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool AccentBorder { get => (bool)GetValue(AccentBorderProperty); set => SetValue(AccentBorderProperty, value); }

    /// <summary>Обрезать ли содержимое по скруглённой форме (строки с подсветкой до краёв).</summary>
    public static readonly DependencyProperty ClipContentProperty = DependencyProperty.Register(
        nameof(ClipContent), typeof(bool), typeof(GlassPanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsArrange));

    public bool ClipContent { get => (bool)GetValue(ClipContentProperty); set => SetValue(ClipContentProperty, value); }

    #endregion

    #region recipe colors (выставляются неявным стилем App.xaml из темы)

    private static DependencyProperty ColorProp(string name) => DependencyProperty.Register(
        name, typeof(Color), typeof(GlassPanel),
        new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlassColorProperty = ColorProp(nameof(GlassColor));
    public static readonly DependencyProperty GlassStrongColorProperty = ColorProp(nameof(GlassStrongColor));
    public static readonly DependencyProperty GlassFaintColorProperty = ColorProp(nameof(GlassFaintColor));
    public static readonly DependencyProperty SpotColorProperty = ColorProp(nameof(SpotColor));
    public static readonly DependencyProperty GlossColorProperty = ColorProp(nameof(GlossColor));
    public static readonly DependencyProperty RimAColorProperty = ColorProp(nameof(RimAColor));
    public static readonly DependencyProperty RimBColorProperty = ColorProp(nameof(RimBColor));
    public static readonly DependencyProperty RimCColorProperty = ColorProp(nameof(RimCColor));
    public static readonly DependencyProperty RimDColorProperty = ColorProp(nameof(RimDColor));
    public static readonly DependencyProperty InnerTopColorProperty = ColorProp(nameof(InnerTopColor));
    public static readonly DependencyProperty InnerBottomColorProperty = ColorProp(nameof(InnerBottomColor));
    public static readonly DependencyProperty ShadowColorProperty = ColorProp(nameof(ShadowColor));
    public static readonly DependencyProperty AccentColorProperty = ColorProp(nameof(AccentColor));

    public Color GlassColor { get => (Color)GetValue(GlassColorProperty); set => SetValue(GlassColorProperty, value); }
    public Color GlassStrongColor { get => (Color)GetValue(GlassStrongColorProperty); set => SetValue(GlassStrongColorProperty, value); }
    public Color GlassFaintColor { get => (Color)GetValue(GlassFaintColorProperty); set => SetValue(GlassFaintColorProperty, value); }
    public Color SpotColor { get => (Color)GetValue(SpotColorProperty); set => SetValue(SpotColorProperty, value); }
    public Color GlossColor { get => (Color)GetValue(GlossColorProperty); set => SetValue(GlossColorProperty, value); }
    public Color RimAColor { get => (Color)GetValue(RimAColorProperty); set => SetValue(RimAColorProperty, value); }
    public Color RimBColor { get => (Color)GetValue(RimBColorProperty); set => SetValue(RimBColorProperty, value); }
    public Color RimCColor { get => (Color)GetValue(RimCColorProperty); set => SetValue(RimCColorProperty, value); }
    public Color RimDColor { get => (Color)GetValue(RimDColorProperty); set => SetValue(RimDColorProperty, value); }
    public Color InnerTopColor { get => (Color)GetValue(InnerTopColorProperty); set => SetValue(InnerTopColorProperty, value); }
    public Color InnerBottomColor { get => (Color)GetValue(InnerBottomColorProperty); set => SetValue(InnerBottomColorProperty, value); }
    public Color ShadowColor { get => (Color)GetValue(ShadowColorProperty); set => SetValue(ShadowColorProperty, value); }
    public Color AccentColor { get => (Color)GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }

    #endregion

    #region visual children (контент + верхний слой с кромкой)

    protected override int VisualChildrenCount => base.VisualChildrenCount + 1;

    protected override Visual GetVisualChild(int index)
    {
        var baseCount = base.VisualChildrenCount;
        return index < baseCount ? base.GetVisualChild(index) : _overlay;
    }

    #endregion

    #region layout

    protected override Size MeasureOverride(Size constraint)
    {
        var p = Padding;
        var ph = p.Left + p.Right;
        var pv = p.Top + p.Bottom;
        if (Child == null) return new Size(ph, pv);
        Child.Measure(new Size(Math.Max(0, constraint.Width - ph), Math.Max(0, constraint.Height - pv)));
        var d = Child.DesiredSize;
        return new Size(d.Width + ph, d.Height + pv);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child != null)
        {
            var p = Padding;
            var inner = new Rect(p.Left, p.Top,
                Math.Max(0, arrangeSize.Width - p.Left - p.Right),
                Math.Max(0, arrangeSize.Height - p.Top - p.Bottom));
            Child.Arrange(inner);
            if (ClipContent)
            {
                // Clip задаётся в координатах ребёнка, а они сдвинуты не только на Padding, но
                // и на его собственный Margin/выравнивание — иначе скругление срезало бы угол
                // содержимого (первую букву заголовка у карточки с <StackPanel Margin="14,13">).
                // Итоговый сдвиг известен сразу после Arrange.
                var r = EffectiveRadius(arrangeSize);
                var off = VisualTreeHelper.GetOffset(Child);
                Child.Clip = new RectangleGeometry(new Rect(-off.X, -off.Y, arrangeSize.Width, arrangeSize.Height), r, r);
            }
            else
            {
                Child.ClearValue(ClipProperty);
            }
        }
        return arrangeSize;
    }

    private double EffectiveRadius(Size size) =>
        Math.Max(0, Math.Min(CornerRadius, Math.Min(size.Width, size.Height) / 2));

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var size = RenderSize;
        var r = EffectiveRadius(size);
        var shape = new RectangleGeometry(new Rect(size), r, r);
        return shape.FillContains(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null;
    }

    #endregion

    #region rendering

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        var rect = new Rect(size);
        var r = EffectiveRadius(size);
        var shape = new RectangleGeometry(rect, r, r);
        shape.Freeze();

        DrawShadow(dc, rect, r, shape);

        var fill = Level switch
        {
            GlassLevel.Strong => GlassStrongColor,
            GlassLevel.Faint => GlassFaintColor,
            _ => GlassColor
        };
        dc.DrawGeometry(Frozen(new SolidColorBrush(fill)), null, shape);
        if (Tint != null) dc.DrawGeometry(Tint, null, shape);

        dc.PushClip(shape);

        // Блик — источник света на стеклянной поверхности (LocalGlassLight в Android). В эталоне
        // он гуляет x .15→.85, y .05→.30 за 7 с, но здесь стоит неподвижно в верхней левой
        // трети: блик есть на каждой карточке, и его движение держало рендер WPF активным,
        // перерисовывая все панели 30 раз в секунду (основная нагрузка на процессор в простое).
        var spot = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            RadiusX = 240,
            RadiusY = 240,
            GradientStops =
            {
                new GradientStop(SpotColor, 0),
                new GradientStop(WithAlpha(SpotColor, 0), 1)
            }
        };
        var light = new Point(size.Width * 0.32, size.Height * 0.11);
        spot.Center = light;
        spot.GradientOrigin = light;
        spot.Freeze();
        dc.DrawRectangle(spot, null, rect);

        // Диагональный глянец → прозрачный к 45%.
        dc.DrawRectangle(Frozen(new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(GlossColor, 0),
            new GradientStop(WithAlpha(GlossColor, 0), 0.45)
        }, new Point(0, 0), new Point(1, 1))), null, rect);

        // Внутренняя светлая тень сверху (14px) и тёмная снизу.
        const double innerH = 14;
        var innerTop = WithAlpha(InnerTopColor, (byte)(InnerTopColor.A * 0.45));
        dc.DrawRectangle(Frozen(new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(innerTop, 0),
            new GradientStop(WithAlpha(innerTop, 0), 1)
        }, new Point(0, 0), new Point(0, 1))), null, new Rect(0, 0, size.Width, Math.Min(innerH, size.Height)));
        var bottomH = Math.Min(innerH * 1.3, size.Height);
        dc.DrawRectangle(Frozen(new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(WithAlpha(InnerBottomColor, 0), 0),
            new GradientStop(InnerBottomColor, 1)
        }, new Point(0, 0), new Point(0, 1))), null, new Rect(0, size.Height - bottomH, size.Width, bottomH));

        dc.Pop();

        RenderOverlay(size, r);
    }

    /// <summary>Кромка и акцентная обводка — отдельным визуальным слоем ПОВЕРХ контента (как
    /// drawContent() → drawOutline() в Android), иначе подсветка строк до краёв её перекрывала бы.</summary>
    private void RenderOverlay(Size size, double r)
    {
        using var dc = _overlay.RenderOpen();
        var edge = new Rect(0.5, 0.5, Math.Max(0, size.Width - 1), Math.Max(0, size.Height - 1));
        var er = Math.Max(0, r - 0.5);
        var rim = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(size.Width * 0.82, size.Height),
            GradientStops =
            {
                new GradientStop(RimAColor, 0),
                new GradientStop(RimBColor, 0.35),
                new GradientStop(RimCColor, 0.62),
                new GradientStop(RimDColor, 1)
            }
        };
        rim.Freeze();
        var rimPen = new Pen(rim, 1);
        rimPen.Freeze();
        dc.DrawRoundedRectangle(null, rimPen, edge, er, er);
        if (AccentBorder)
        {
            var accent = new Pen(Frozen(new SolidColorBrush(AccentColor)), 1.5);
            accent.Freeze();
            var ar = new Rect(0.75, 0.75, Math.Max(0, size.Width - 1.5), Math.Max(0, size.Height - 1.5));
            dc.DrawRoundedRectangle(null, accent, ar, Math.Max(0, r - 0.75), Math.Max(0, r - 0.75));
        }
    }

    /// <summary>Мягкая тень только снаружи формы. Вместо размытия (эффекты в WPF дорогие при
    /// анимированном стекле сверху) — стопка вложенных скруглённых прямоугольников со слабой
    /// альфой: накопленная альфа линейно спадает от полной внутри до нуля на расстоянии blur/2
    /// от края смещённой на dy формы — визуально то же, что setShadowLayer(blur, 0, dy) в Android.</summary>
    private void DrawShadow(DrawingContext dc, Rect rect, double r, Geometry shape)
    {
        var (blur, dy) = Shadow switch
        {
            GlassShadow.Card => (22.0, 10.0),
            GlassShadow.Strong => (26.0, 12.0),
            GlassShadow.Pill => (12.0, 5.0),
            _ => (0.0, 0.0)
        };
        if (blur <= 0 || ShadowColor.A == 0) return;

        var outer = new RectangleGeometry(Rect.Inflate(rect, blur + dy + 4, blur + dy + 4));
        var clip = new CombinedGeometry(GeometryCombineMode.Exclude, outer, shape);
        clip.Freeze();
        dc.PushClip(clip);

        const int steps = 10;
        var stepAlpha = (byte)Math.Max(1, ShadowColor.A / (steps + 1));
        var stepBrush = Frozen(new SolidColorBrush(WithAlpha(ShadowColor, stepAlpha)));
        var baseRect = new Rect(rect.X, rect.Y + dy, rect.Width, rect.Height);
        for (var k = 0; k <= steps; k++)
        {
            var inflate = -blur / 2 + blur * k / steps;
            var rr = Rect.Inflate(baseRect, inflate, inflate);
            if (rr.Width <= 0 || rr.Height <= 0) continue;
            var rad = Math.Max(0, r + inflate);
            dc.DrawRoundedRectangle(stepBrush, null, rr, rad, rad);
        }

        // Контактная тень у самой кромки (blur 2, dy 1, альфа ×0.6).
        var contact = Frozen(new SolidColorBrush(WithAlpha(ShadowColor, (byte)(ShadowColor.A * 0.6 / 2))));
        var cr = new Rect(rect.X, rect.Y + 1, rect.Width, rect.Height);
        dc.DrawRoundedRectangle(contact, null, Rect.Inflate(cr, 0.5, 0.5), r + 0.5, r + 0.5);
        dc.DrawRoundedRectangle(contact, null, Rect.Inflate(cr, 1.5, 1.5), r + 1.5, r + 1.5);

        dc.Pop();
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    #endregion
}
