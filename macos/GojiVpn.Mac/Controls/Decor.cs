using GodjiVpn.Utils;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace GodjiVpn.Controls;

/// <summary>Базовый контрол, перерисовывающийся по тику общих часов, пока он в дереве.</summary>
public abstract class AnimatedControl : Control
{
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

    protected IBrush Res(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

    protected Color ResColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is Color c ? c : fallback;

    protected static double Seconds => GlassClock.Time.Elapsed.TotalSeconds;
}

/// <summary>
/// Кнопка подключения 80×80 — порт PowerButton Windows-клиента: выключено — стеклянный круг,
/// подключение — вращающаяся дуга Teal/Terracotta вокруг, включено — круг с акцентным
/// градиентом, свечением и двумя расходящимися кольцами.
/// </summary>
public class PowerButton : Panel
{
    public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<PowerButton, bool>(nameof(IsOn));
    public static readonly StyledProperty<bool> IsBusyProperty = AvaloniaProperty.Register<PowerButton, bool>(nameof(IsBusy));
    public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<PowerButton, ICommand?>(nameof(Command));

    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    private readonly Fx _fx;
    private readonly GlassPanel _glass = new() { CornerRadius = 999, Level = GlassLevel.Strong, Shadow = GlassShadow.Strong };
    private readonly Accent _accent = new() { IsHitTestVisible = false };
    private readonly Avalonia.Controls.Shapes.Path _icon = new() { Width = 28, Height = 28, Stretch = Stretch.Uniform, IsHitTestVisible = false };

    public PowerButton()
    {
        Width = 80;
        Height = 80;
        HorizontalAlignment = HorizontalAlignment.Center;
        Cursor = new Cursor(StandardCursorType.Hand);
        _fx = new Fx(this) { IsHitTestVisible = false };
        Children.Add(_fx);
        Children.Add(_glass);
        Children.Add(_accent);
        _icon[!Avalonia.Controls.Shapes.Path.DataProperty] = new DynamicResourceExtension("IconPower");
        Children.Add(_icon);
        RenderTransform = new ScaleTransform(1, 1);
        PointerPressed += (_, _) => RenderTransform = new ScaleTransform(0.94, 0.94);
        PointerReleased += (_, e) =>
        {
            RenderTransform = new ScaleTransform(1, 1);
            if (e.InitialPressMouseButton == MouseButton.Left && Command?.CanExecute(null) == true) Command.Execute(null);
        };
        PointerCaptureLost += (_, _) => RenderTransform = new ScaleTransform(1, 1);
        UpdateState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty || change.Property == IsBusyProperty) UpdateState();
    }

    private void UpdateState()
    {
        _glass.IsVisible = !IsOn;
        _accent.IsVisible = IsOn;
        if (IsOn) _icon.Fill = Brushes.White;
        else _icon[!Avalonia.Controls.Shapes.Path.FillProperty] = new DynamicResourceExtension("TextPrimaryBrush");
    }

    private sealed class Accent : AnimatedControl
    {
        public override void Render(DrawingContext ctx)
        {
            var rect = new Rect(Bounds.Size);
            var glow = ResColor("AccentGlowColor", Color.Parse("#5900A79B"));
            ctx.DrawRectangle(Res("AccentGradientBrush", Brushes.Teal), new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1),
                new RoundedRect(rect, rect.Width / 2), new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 22, Color = glow }));
        }
    }

    private sealed class Fx : AnimatedControl
    {
        private readonly PowerButton _owner;

        public Fx(PowerButton owner) => _owner = owner;

        public override void Render(DrawingContext ctx)
        {
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var teal = Res("TealBrush", Brushes.Teal);
            if (_owner.IsOn)
            {
                ctx.DrawEllipse(Res("TealTintBrush", Brushes.Transparent), null, c, 48, 48);
                var tealColor = ResColor("TealColor", Colors.Teal);
                for (var i = 0; i < 2; i++)
                {
                    var p = (Seconds / 2.4 + i * 0.5) % 1;
                    var eased = 1 - (1 - p) * (1 - p);
                    var r = 40 * (1 + 0.55 * eased);
                    var alpha = (byte)(0.5 * (1 - eased) * 255);
                    ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(alpha, tealColor.R, tealColor.G, tealColor.B)), 2), c, r, r);
                }
            }
            else if (_owner.IsBusy)
            {
                var angle = Seconds * 360 % 360;
                const double r = 45 - 1.25;
                DrawArc(ctx, c, r, 225 + angle, 90, teal);
                DrawArc(ctx, c, r, 315 + angle, 90, Res("TerracottaBrush", Brushes.OrangeRed));
            }
        }

        private static void DrawArc(DrawingContext ctx, Point c, double r, double startDeg, double sweepDeg, IBrush brush)
        {
            Point P(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
            var g = new StreamGeometry();
            using (var s = g.Open())
            {
                s.BeginFigure(P(startDeg), false);
                s.ArcTo(P(startDeg + sweepDeg), new Size(r, r), 0, false, SweepDirection.Clockwise);
                s.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(brush, 2.5), g);
        }
    }
}

/// <summary>Полоса прогресса 8px: дорожка TrackBg, заливка акцентным градиентом, бегущий
/// блик по заливке (трафик, загрузка обновления).</summary>
public class ShimmerBar : AnimatedControl
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ShimmerBar, double>(nameof(Value));
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    private double _shown;

    public ShimmerBar() => Height = 8;

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0) return;
        _shown += (Math.Clamp(Value, 0, 1) - _shown) * 0.2;
        ctx.DrawRectangle(Res("TrackBgBrush", Brushes.LightGray), null, new RoundedRect(new Rect(0, 0, w, h), h / 2));
        var fw = w * _shown;
        if (fw < 1) return;
        var fill = new RoundedRect(new Rect(0, 0, Math.Max(fw, h), h), h / 2);
        ctx.DrawRectangle(Res("AccentGradientBrush", Brushes.Teal), null, fill);
        using (ctx.PushClip(fill))
        {
            var x = (Seconds / 2.2 % 1) * (fw + 60) - 60;
            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(110, 255, 255, 255), 0.5),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                }
            }, null, new Rect(x, 0, 60, h));
        }
    }
}

/// <summary>Кольцо "осталось N дней" 68×68: дуга Teal (доля days/30) по дорожке, ядро RingCore
/// с числом и "ДНЕЙ".</summary>
public class DaysRing : Panel
{
    public static readonly StyledProperty<int> DaysProperty = AvaloniaProperty.Register<DaysRing, int>(nameof(Days));
    public int Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }

    private readonly TextBlock _number = new() { FontWeight = FontWeight.ExtraBold, [!TextBlock.FontSizeProperty] = FontScale.For(19), HorizontalAlignment = HorizontalAlignment.Center };

    public DaysRing()
    {
        Width = 68;
        Height = 68;
        Children.Add(new Ring(this));
        var core = new Border { Width = 54, Height = 54, CornerRadius = new CornerRadius(27) };
        core[!Border.BackgroundProperty] = new DynamicResourceExtension("RingCoreBrush");
        Children.Add(core);
        var label = new TextBlock { Text = "ДНЕЙ", FontWeight = FontWeight.Bold, [!TextBlock.FontSizeProperty] = FontScale.For(7.5), HorizontalAlignment = HorizontalAlignment.Center };
        label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextSecondaryBrush");
        _number[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextPrimaryBrush");
        Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _number, label }
        });
        _number.Text = "0";
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DaysProperty)
        {
            _number.Text = Days.ToString();
            foreach (var ch in Children) if (ch is Ring r) r.InvalidateVisual();
        }
    }

    private sealed class Ring : Control
    {
        private readonly DaysRing _owner;

        public Ring(DaysRing owner) => _owner = owner;

        public override void Render(DrawingContext ctx)
        {
            var size = Math.Min(Bounds.Width, Bounds.Height);
            var r = size / 2 - 3.5;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var track = this.TryFindResource("TrackBgBrush", ActualThemeVariant, out var t) && t is IBrush tb ? tb : Brushes.LightGray;
            var teal = this.TryFindResource("TealBrush", ActualThemeVariant, out var b) && b is IBrush bb ? bb : Brushes.Teal;
            ctx.DrawEllipse(null, new Pen(track, 7), c, r, r);
            var fraction = Math.Clamp(_owner.Days / 30.0, 0, 1);
            if (fraction <= 0) return;
            if (fraction >= 0.999)
            {
                ctx.DrawEllipse(null, new Pen(teal, 7), c, r, r);
                return;
            }
            Point P(double deg) => new(c.X + r * Math.Cos(deg * Math.PI / 180), c.Y + r * Math.Sin(deg * Math.PI / 180));
            var g = new StreamGeometry();
            using (var s = g.Open())
            {
                s.BeginFigure(P(-90), false);
                s.ArcTo(P(-90 + 360 * fraction), new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise);
                s.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(teal, 7, lineCap: PenLineCap.Round), g);
        }
    }
}

/// <summary>"Живой" бейдж статуса ("АКТИВНА"): капсула с ядром RingCore, по кромке бежит
/// комета акцентного цвета (конический градиент), внутри — пульсирующая точка и подпись.</summary>
public class ActiveBadge : Panel
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<ActiveBadge, string>(nameof(Text), "АКТИВНА");
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private readonly TextBlock _label = new() { FontWeight = FontWeight.ExtraBold, [!TextBlock.FontSizeProperty] = FontScale.For(10), VerticalAlignment = VerticalAlignment.Center };

    public ActiveBadge()
    {
        Height = 26;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(new Comet());
        var core = new Border { Margin = new Thickness(1.5), CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 0, 12, 0) };
        core[!Border.BackgroundProperty] = new DynamicResourceExtension("RingCoreBrush");
        _label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TealDeepBrush");
        _label.Text = Text;
        core.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { new Dot(), _label }
        };
        Children.Add(core);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) _label.Text = Text;
    }

    private sealed class Comet : AnimatedControl
    {
        public override void Render(DrawingContext ctx)
        {
            var rect = new Rect(Bounds.Size);
            if (rect.Width <= 0) return;
            var teal = ResColor("TealColor", Colors.Teal);
            var brush = new ConicGradientBrush
            {
                Angle = Seconds * 360 / 2.6 % 360,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, teal.R, teal.G, teal.B), 0),
                    new GradientStop(Color.FromArgb(0, teal.R, teal.G, teal.B), 0.55),
                    new GradientStop(teal, 0.95),
                    new GradientStop(Color.FromArgb(0, teal.R, teal.G, teal.B), 1)
                }
            };
            ctx.DrawRectangle(Res("CardBorderBrush", Brushes.White), null, new RoundedRect(rect, rect.Height / 2));
            ctx.DrawRectangle(brush, null, new RoundedRect(rect, rect.Height / 2));
        }
    }

    private sealed class Dot : AnimatedControl
    {
        public Dot()
        {
            Width = 7;
            Height = 7;
            VerticalAlignment = VerticalAlignment.Center;
        }

        public override void Render(DrawingContext ctx)
        {
            var c = new Point(3.5, 3.5);
            var teal = ResColor("TealColor", Colors.Teal);
            var p = Seconds / 1.6 % 1;
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(90 * (1 - p)), teal.R, teal.G, teal.B)), null, c, 3.5 + 3 * p, 3.5 + 3 * p);
            ctx.DrawEllipse(new SolidColorBrush(teal), null, c, 3.5, 3.5);
        }
    }
}

/// <summary>Голографический бейдж ("Текущий", "НОВАЯ"): переливающийся градиент с бликом.</summary>
public class HoloBadge : Panel
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<HoloBadge, string>(nameof(Text), "");
    public static readonly StyledProperty<string> IconProperty = AvaloniaProperty.Register<HoloBadge, string>(nameof(Icon), "★");
    public static readonly StyledProperty<bool> ReverseProperty = AvaloniaProperty.Register<HoloBadge, bool>(nameof(Reverse));

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public bool Reverse { get => GetValue(ReverseProperty); set => SetValue(ReverseProperty, value); }

    private static readonly Color[] Holo =
    {
        Color.FromRgb(0x1F, 0xC2, 0xB2), Color.FromRgb(0x7C, 0x8C, 0xFF), Color.FromRgb(0xE5, 0x8F, 0xD0),
        Color.FromRgb(0xF4, 0xC2, 0x7A), Color.FromRgb(0x1F, 0xC2, 0xB2)
    };

    private readonly TextBlock _label = new()
    {
        FontWeight = FontWeight.ExtraBold, [!TextBlock.FontSizeProperty] = FontScale.For(10), Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0)
    };

    public HoloBadge()
    {
        Height = 20;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(new Shine(this));
        Children.Add(_label);
        UpdateText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IconProperty) UpdateText();
    }

    private void UpdateText() => _label.Text = string.IsNullOrEmpty(Icon) ? Text : $"{Icon} {Text}";

    private sealed class Shine : AnimatedControl
    {
        private readonly HoloBadge _owner;

        public Shine(HoloBadge owner) => _owner = owner;

        public override void Render(DrawingContext ctx)
        {
            var rect = new Rect(Bounds.Size);
            if (rect.Width <= 0) return;
            var colors = _owner.Reverse ? Enumerable.Reverse(Holo).ToArray() : Holo;
            var shift = Seconds / 4 % 1;
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(-shift, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(2 - shift, 0, RelativeUnit.Relative),
                SpreadMethod = GradientSpreadMethod.Repeat
            };
            for (var i = 0; i < colors.Length; i++) brush.GradientStops.Add(new GradientStop(colors[i], i / (double)(colors.Length - 1)));
            var shape = new RoundedRect(rect, rect.Height / 2);
            ctx.DrawRectangle(brush, null, shape);
            using (ctx.PushClip(shape))
            {
                var x = (Seconds / 3 % 1) * (rect.Width + 40) - 40;
                ctx.DrawRectangle(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                        new GradientStop(Color.FromArgb(178, 255, 255, 255), 0.5),
                        new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                    }
                }, null, new Rect(x, 0, 40, rect.Height));
            }
        }
    }
}

/// <summary>Медленный диагональный блик по карточке подписки (раз в 6 с).</summary>
public class SweepShine : AnimatedControl
{
    public SweepShine() => IsHitTestVisible = false;

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var t = Seconds % 6 / 2.2;
        if (t > 1) return;
        var band = w * 0.26;
        var x = -band + (w + band * 2) * t;
        using (ctx.PushTransform(Matrix.CreateTranslation(x, 0) * Matrix.CreateRotation(-20 * Math.PI / 180)))
        {
            ctx.DrawRectangle(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(56, 255, 255, 255), 0.5),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                }
            }, null, new Rect(-band / 2, -h, band, h * 3));
        }
    }
}
