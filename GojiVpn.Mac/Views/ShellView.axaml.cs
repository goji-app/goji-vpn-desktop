using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using GodjiVpn.ViewModels;

namespace GodjiVpn.Views;

public partial class ShellView : UserControl
{
    private const int TabCount = 4;
    private ShellViewModel? _vm;
    private bool _placed;

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = DataContext as ShellViewModel;
            if (_vm != null) _vm.PropertyChanged += OnVmPropertyChanged;
            PlaceLens(animate: false);
        };
        TabHost.SizeChanged += (_, _) => PlaceLens(animate: false);
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (_vm != null && sender is Control { Tag: string tag } && int.TryParse(tag, out var index))
            _vm.SelectedTabIndex = index;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.SelectedTabIndex)) PlaceLens(animate: true);
    }

    /// <summary>"Линза" под выбранной вкладкой — едет с лёгкой пружиной.</summary>
    private void PlaceLens(bool animate)
    {
        var width = TabHost.Bounds.Width;
        if (width <= 0) return;
        var slot = width / TabCount;
        Lens.Width = slot;
        var index = Math.Clamp(_vm?.SelectedTabIndex ?? 0, 0, TabCount - 1);
        Lens.Transitions = animate && _placed
            ? new Transitions { new ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromMilliseconds(420), Easing = new BackEaseOut() } }
            : null;
        Lens.Margin = new Thickness(slot * index, 0, 0, 0);
        _placed = true;
    }
}
