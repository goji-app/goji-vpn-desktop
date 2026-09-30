using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace GodjiVpn;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (Environment.GetEnvironmentVariable("GODJI_UI_PREVIEW_THEME") == "dark") RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new Views.DesignTestWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
