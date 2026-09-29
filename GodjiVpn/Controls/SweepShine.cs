using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace GodjiVpn.Controls;

/// <summary>Медленный косой блик по карточке подписки — gg-sweep из эталона (PlansScreen.kt
/// SubscriptionCard): полоса шириной 26% карточки под углом −20°, белая .28 в центре, проходит
/// от −160% до 420% своей ширины за 3.3 с и стоит до конца 6-секундного цикла. Кладётся первым
/// слоем внутрь карточки (обрезается её формой), под содержимое.</summary>
public class SweepShine : Canvas
{
    private readonly Rectangle _band = new() { IsHitTestVisible = false };
    private readonly TranslateTransform _x = new();

    public SweepShine()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        _band.Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 0),
            new GradientStop(Color.FromArgb(71, 255, 255, 255), 0.5),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
        }, new Point(0, 0), new Point(1, 0));
        _band.RenderTransform = new TransformGroup { Children = { new RotateTransform(-20), _x } };
        Children.Add(_band);
        SizeChanged += (_, _) => Restart();
    }

    private void Restart()
    {
        var w = ActualWidth * 0.26;
        var h = ActualHeight * 1.8;
        if (w <= 0) return;
        _band.Width = w;
        _band.Height = h;
        SetTop(_band, -ActualHeight * 0.4);
        var anim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(6), RepeatBehavior = RepeatBehavior.Forever };
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(-1.6 * w, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(4.2 * w, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3300)),
            new CubicEase { EasingMode = EasingMode.EaseInOut }));
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(4.2 * w, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(6))));
        Timeline.SetDesiredFrameRate(anim, 30);
        _x.BeginAnimation(TranslateTransform.XProperty, anim);
    }
}
