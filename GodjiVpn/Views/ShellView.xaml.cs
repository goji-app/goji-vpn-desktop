using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GodjiVpn.ViewModels;

namespace GodjiVpn.Views;

public partial class ShellView : UserControl
{
    private const int TabCount = 4;

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ShellViewModel oldVm) oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is ShellViewModel newVm) newVm.PropertyChanged += OnVmPropertyChanged;
            PlaceLens(animate: false);
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.SelectedTabIndex)) PlaceLens(animate: true);
    }

    private void OnTabHostSizeChanged(object sender, SizeChangedEventArgs e) => PlaceLens(animate: false);

    /// <summary>"Линза" под выбранной вкладкой — spring(0.62, 380) из Android ≈ BackEase с
    /// небольшим перелётом.</summary>
    private void PlaceLens(bool animate)
    {
        var width = TabHost.ActualWidth;
        if (width <= 0) return;
        var slot = width / TabCount;
        Lens.Width = slot;
        var index = (DataContext as ShellViewModel)?.SelectedTabIndex ?? 0;
        var x = slot * Math.Clamp(index, 0, TabCount - 1);
        if (animate)
        {
            LensX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            });
        }
        else
        {
            LensX.BeginAnimation(TranslateTransform.XProperty, null);
            LensX.X = x;
        }
    }
}
