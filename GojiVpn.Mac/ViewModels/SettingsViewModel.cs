using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodjiVpn.Services;
using GodjiVpn.Utils;
using GodjiVpn.Views;

namespace GodjiVpn.ViewModels;

public sealed partial class LogFileItem : ObservableObject
{
    public required string Label { get; init; }
    public required string FileName { get; init; }
    [ObservableProperty] private bool isSelected;
}

public sealed partial class PingMethodItem : ObservableObject
{
    public required string Label { get; init; }
    public required PingMethod Method { get; init; }
    [ObservableProperty] private bool isSelected;
}

public sealed partial class ThemeModeItem : ObservableObject
{
    public required string Label { get; init; }
    public required ThemeMode Mode { get; init; }
    [ObservableProperty] private bool isSelected;
}

public sealed class BypassDomainItem
{
    public required string Domain { get; init; }
    public required string Shown { get; init; }
}

/// <summary>Подэкраны Настроек — в Android это отдельные маршруты навигации
/// (PingSettingsScreen, LogViewerDialog); здесь — подмена содержимого вкладки.</summary>
public enum SettingsPage { Main, Ping, Log, Bypass }

/// <summary>Аналог SettingsScreen.kt — версия/HWID (About), просмотр логов вместо отдельного
/// LogViewerDialog.kt (здесь один экран проще нескольких диалогов на маленьком приложении),
/// тёмная тема, выход из аккаунта. Переключение языка не перенесено — языковой слой (i18n) в
/// Windows-версии пока не заведён вообще, добавлять его только ради пары чипов в Settings — по
/// объёму отдельная большая задача, не часть этого прохода.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly TokenStore _tokenStore;
    private readonly VpnEngine _vpnEngine;
    private readonly HwidProvider _hwid;
    private readonly PingSettings _pingSettings;
    private readonly ThemeService _theme;
    private readonly UpdateService _updateService;
    private readonly ApiClient _api;
    private readonly AppSettings _appSettings;

    private static string LogsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "logs");

    /// <summary>Версия встроенного xray.exe (Assets/xray) — строка "Версия XRAY" в
    /// "О программе", как BUNDLED_XRAY_VERSION в Android.</summary>
    public const string BundledXrayVersion = "26.3.27";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateIdleText))]
    private string appVersion = "—";
    [ObservableProperty] private string hwid = "—";
    [ObservableProperty] private string deviceInfo = "—";
    public string XrayVersion => BundledXrayVersion;
    [ObservableProperty] private LogFileItem selectedLogFile;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLogEmpty))]
    private string logContent = "";
    [ObservableProperty] private string pingTestUrl = "";
    [ObservableProperty] private string? copiedToast;

    // ── Безопасность: проверка утечек (NetworkDiagnostics, порт 72a382d) ──
    private readonly NetworkDiagnostics _diagnostics = new();
    [ObservableProperty] private bool leakChecking;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLeakReport), nameof(LeakOk), nameof(LeakTitle), nameof(LeakDesc),
        nameof(LeakSiteIp), nameof(LeakRealIp), nameof(LeakDns))]
    private NetworkDiagnostics.Report? leakReport;

    public bool HasLeakReport => LeakReport != null && !LeakChecking;
    public bool LeakOk => LeakReport?.Verdict == NetworkDiagnostics.Verdict.Safe;
    public string LeakTitle => LeakReport?.Verdict switch
    {
        NetworkDiagnostics.Verdict.Safe => "Всё защищено",
        NetworkDiagnostics.Verdict.Leak => "Найдена утечка",
        NetworkDiagnostics.Verdict.VpnOff => "VPN выключен",
        _ => "Не удалось проверить"
    };
    public string LeakDesc
    {
        get
        {
            if (LeakReport is not { } r) return "";
            return r.Verdict switch
            {
                NetworkDiagnostics.Verdict.Safe => "Сайты видят IP сервера VPN, DNS-запросы тоже идут через туннель.",
                NetworkDiagnostics.Verdict.VpnOff => "Сайты и провайдер видят твой настоящий IP. Включи VPN и проверь ещё раз.",
                NetworkDiagnostics.Verdict.Error => "Через туннель не пришёл ответ. Проверь соединение и попробуй ещё раз.",
                _ => string.Join(" ", new[]
                {
                    r.IpLeak ? "Сайты видят твой настоящий IP." : null,
                    !r.DnsLeak ? null : r.DnsViaIsp
                        ? "DNS-запросы идут через твоего провайдера" + (r.Dns?.Isp is { } isp ? $" ({isp})" : "") + " — он видит, какие сайты ты открываешь."
                        : "DNS-запросы разрешает сервер в твоей стране" + (r.Dns?.Isp is { } res ? $" ({res})" : "") + ", а не на стороне VPN — по ним видно, какие сайты ты открываешь."
                }.Where(x => x != null))
            };
        }
    }
    public string? LeakSiteIp => LeakReport?.VpnIp is { } v ? Join(v.Ip, v.Country) : null;
    public string? LeakRealIp => LeakReport?.RealIp is { } v ? Join(v.Ip, v.Country) : null;
    public string? LeakDns => LeakReport?.Dns is { } d ? (Join(d.Isp, d.Country) is { Length: > 0 } s ? s : d.Ip) : null;

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    partial void OnLeakCheckingChanged(bool value) => OnPropertyChanged(nameof(HasLeakReport));

    // AllowConcurrentExecutions: повтор отсекает LeakChecking, а кнопка во время проверки не
    // тускнеет как выключенная — в ней крутится спиннер "Проверяем…".
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CheckLeakAsync()
    {
        if (LeakChecking) return;
        LeakChecking = true;
        try { LeakReport = await _diagnostics.CheckAsync(); }
        catch { LeakReport = new NetworkDiagnostics.Report(NetworkDiagnostics.Verdict.Error, null, null, null, false, false, false); }
        finally { LeakChecking = false; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainPage), nameof(IsPingPage), nameof(IsLogPage), nameof(IsBypassPage))]
    private SettingsPage page = SettingsPage.Main;

    public bool IsMainPage => Page == SettingsPage.Main;
    public bool IsPingPage => Page == SettingsPage.Ping;
    public bool IsLogPage => Page == SettingsPage.Log;
    public bool IsBypassPage => Page == SettingsPage.Bypass;
    public bool IsLogEmpty => string.IsNullOrWhiteSpace(LogContent);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableUpdate), nameof(ShowUpdateAvailable), nameof(ShowUpdateChecking),
        nameof(ShowUpdateIdle), nameof(HasUpdateChangelog))]
    private UpdateInfo? availableUpdate;
    [ObservableProperty] private string updateChangelogText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateChecking), nameof(ShowUpdateIdle))]
    private bool isCheckingUpdate;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateAvailable), nameof(ShowUpdateChecking), nameof(ShowUpdateIdle))]
    private bool isDownloadingUpdate;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadPercentLabel))]
    private double downloadProgress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateIdleText))]
    private string? updateCheckMessage;

    public bool HasAvailableUpdate => AvailableUpdate != null;
    public bool HasUpdateChangelog => !string.IsNullOrWhiteSpace(AvailableUpdate?.Changelog);

    /// <summary>Состояния блока "Обновления" по эталону (UpdateSectionContent): загрузка →
    /// найдено → проверка → покой. В каждый момент видно ровно одно.</summary>
    public bool ShowUpdateAvailable => !IsDownloadingUpdate && HasAvailableUpdate;
    public bool ShowUpdateChecking => !IsDownloadingUpdate && !HasAvailableUpdate && IsCheckingUpdate;
    public bool ShowUpdateIdle => !IsDownloadingUpdate && !HasAvailableUpdate && !IsCheckingUpdate;
    public string UpdateIdleText => UpdateCheckMessage ?? $"Версия {AppVersion}";
    public string DownloadPercentLabel => $"{(int)Math.Round(DownloadProgress * 100)}%";

    /// <summary>Индекс для сегмент-контрола темы (Светлая/Тёмная/Системная).</summary>
    public int ThemeIndex
    {
        get
        {
            for (var i = 0; i < ThemeModes.Count; i++)
                if (ThemeModes[i].Mode == _theme.Mode) return i;
            return 0;
        }
        set
        {
            if (value < 0 || value >= ThemeModes.Count || ThemeModes[value].Mode == _theme.Mode) return;
            SelectThemeMode(ThemeModes[value]);
        }
    }

    public ObservableCollection<LogFileItem> LogFiles { get; } = new()
    {
        new LogFileItem { Label = "Приложение", FileName = "engine.log" },
        new LogFileItem { Label = "VPN-ядро (xray)", FileName = "xray.log" },
        new LogFileItem { Label = "VPN-туннель (sing-box)", FileName = "sing-box.log" },
        new LogFileItem { Label = "Сбои", FileName = "crash.log" },
    };

    /// <summary>Аналог PingSettingsScreen.kt — способ проверки серверов на вкладке "Серверы"
    /// (см. PingService/PingSettings).</summary>
    public ObservableCollection<PingMethodItem> PingMethods { get; } = new()
    {
        new PingMethodItem { Label = "Через прокси · GET", Method = PingMethod.ProxyGet },
        new PingMethodItem { Label = "Через прокси · HEAD", Method = PingMethod.ProxyHead },
        new PingMethodItem { Label = "TCP", Method = PingMethod.Tcp },
        new PingMethodItem { Label = "ICMP", Method = PingMethod.Icmp },
    };

    /// <summary>Аналог ThemeMode (Android) — "Системная" следует теме Windows живьём, пока
    /// приложение открыто (см. ThemeService.OnUserPreferenceChanged), а не только в момент
    /// выбора.</summary>
    public ObservableCollection<ThemeModeItem> ThemeModes { get; } = new()
    {
        new ThemeModeItem { Label = "Светлая", Mode = ThemeMode.Light },
        new ThemeModeItem { Label = "Тёмная", Mode = ThemeMode.Dark },
        new ThemeModeItem { Label = "Системная", Mode = ThemeMode.System },
    };

    public event Action? RequestLogout;

    public SettingsViewModel(TokenStore tokenStore, VpnEngine vpnEngine, HwidProvider hwid, PingSettings pingSettings,
        ThemeService theme, UpdateService updateService, ApiClient api, AppSettings appSettings)
    {
        _appSettings = appSettings;
        _tokenStore = tokenStore;
        _vpnEngine = vpnEngine;
        _hwid = hwid;
        _pingSettings = pingSettings;
        _theme = theme;
        _updateService = updateService;
        _api = api;
        selectedLogFile = LogFiles[0];
        selectedLogFile.IsSelected = true;
        foreach (var m in PingMethods) m.IsSelected = m.Method == _pingSettings.Method;
        pingTestUrl = _pingSettings.TestUrl;
        foreach (var m in ThemeModes) m.IsSelected = m.Mode == _theme.Mode;
    }

    [RelayCommand]
    private void SelectThemeMode(ThemeModeItem item)
    {
        foreach (var m in ThemeModes) m.IsSelected = m == item;
        _theme.SetMode(item.Mode);
        OnPropertyChanged(nameof(ThemeIndex));
    }

    public void Load()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "—";
        Hwid = _hwid.Get();
        DeviceInfo = BuildDeviceInfo();
        RefreshLog();
    }

    /// <summary>"Об устройстве" — аналог "MANUFACTURER MODEL, Android RELEASE": имя
    /// компьютера и версия Windows. Environment.OSVersion у Windows 11 по-прежнему 10.0,
    /// поэтому 11 определяем по номеру сборки (от 22000).</summary>
    private static string BuildDeviceInfo()
    {
        var v = Environment.OSVersion.Version;
        var name = v.Major == 10 && v.Build >= 22000 ? "Windows 11" : $"Windows {v.Major}.{v.Minor}";
        return $"{Environment.MachineName}, {name} ({v.Build})";
    }

    [RelayCommand]
    private void SelectPingMethod(PingMethodItem item)
    {
        foreach (var m in PingMethods) m.IsSelected = m == item;
        _pingSettings.Method = item.Method;
    }

    [RelayCommand]
    private void OpenPingSettings() => Page = SettingsPage.Ping;

    [RelayCommand]
    private void OpenLog(LogFileItem item)
    {
        SelectLogFile(item);
        Page = SettingsPage.Log;
    }

    [RelayCommand]
    private void Back() => Page = SettingsPage.Main;

    // ── Сайты мимо VPN (порт 6803326) ──
    public ObservableCollection<BypassDomainItem> BypassDomains { get; } = new();
    [ObservableProperty] private string bypassInput = "";
    [ObservableProperty] private bool bypassError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBypassApply))]
    private bool bypassDirty;
    public bool ShowBypassApply => BypassDirty && _vpnEngine.IsRunning;
    public string BypassListLabel => $"САЙТЫ · {BypassDomains.Count}";
    public bool BypassEmpty => BypassDomains.Count == 0;

    partial void OnBypassInputChanged(string value) => BypassError = false;

    [RelayCommand]
    private void OpenBypass()
    {
        RefreshBypass();
        BypassDirty = false;
        Page = SettingsPage.Bypass;
    }

    [RelayCommand]
    private void AddBypass()
    {
        if (string.IsNullOrWhiteSpace(BypassInput)) return;
        var domain = AppSettings.NormalizeDomain(BypassInput);
        if (domain == null) { BypassError = true; return; }
        _appSettings.AddBypassDomain(domain);
        BypassInput = "";
        BypassDirty = true;
        RefreshBypass();
    }

    [RelayCommand]
    private void RemoveBypass(BypassDomainItem item)
    {
        _appSettings.RemoveBypassDomain(item.Domain);
        BypassDirty = true;
        RefreshBypass();
    }

    /// <summary>Правила xray читаются только при подключении — переподключаемся к тому же узлу.</summary>
    [RelayCommand]
    private async Task ApplyBypassAsync()
    {
        BypassDirty = false;
        await _vpnEngine.ReconnectAsync();
    }

    private void RefreshBypass()
    {
        BypassDomains.Clear();
        foreach (var d in _appSettings.BypassDomains)
            BypassDomains.Add(new BypassDomainItem { Domain = d, Shown = AppSettings.ToDisplay(d) });
        OnPropertyChanged(nameof(BypassListLabel));
        OnPropertyChanged(nameof(BypassEmpty));
    }

    /// <summary>Строки "О программе" копируются по нажатию (AboutRow в Android).</summary>
    [RelayCommand]
    private async Task CopyAboutAsync(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "—") return;
        try { Ui.CopyText(value); } catch { return; }
        CopiedToast = "Скопировано";
        await Task.Delay(1600);
        CopiedToast = null;
    }

    partial void OnPingTestUrlChanged(string value) => _pingSettings.TestUrl = value;

    [RelayCommand]
    private void SelectLogFile(LogFileItem item)
    {
        SelectedLogFile = item;
        foreach (var f in LogFiles) f.IsSelected = f == item;
        RefreshLog();
    }

    [RelayCommand]
    private void RefreshLog()
    {
        var path = Path.Combine(LogsDir, SelectedLogFile.FileName);
        try
        {
            if (!File.Exists(path)) { LogContent = ""; return; }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            const int maxChars = 20000;
            var length = (int)Math.Min(stream.Length, maxChars);
            stream.Seek(-length, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            LogContent = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            LogContent = $"Не удалось прочитать лог: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyLog()
    {
        if (string.IsNullOrEmpty(LogContent)) return;
        try { Ui.CopyText(LogContent); } catch { /* буфер занят другим процессом */ }
    }

    [RelayCommand]
    private void CopyHwid() => Ui.CopyText(Hwid);

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(LogsDir);
        ShellLauncher.OpenFolder(LogsDir);
    }

    /// <summary>Новая секция "Поддержка" — тот же нативный чат, что и на вкладке "Подписка"
    /// (см. PlansViewModel.OpenSupport). Порт из Android (720f5ff): там тоже добавлена
    /// отдельная точка входа в SettingsScreen.</summary>
    [RelayCommand]
    private void OpenSupport() => _ = Ui.ShowDialogAsync(new SupportWindow(_api));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (_vpnEngine.IsRunning) await _vpnEngine.DisconnectAsync();
        _tokenStore.Clear();
        RequestLogout?.Invoke();
    }

    /// <summary>Вызывается из App.xaml.cs после фоновой проверки при старте — чтобы не делать
    /// два одинаковых сетевых запроса подряд (фоновый + ручной при первом открытии Настроек),
    /// просто подхватываем уже готовый результат, если он есть.</summary>
    public void ApplyBackgroundUpdateCheck(UpdateInfo? update)
    {
        AvailableUpdate = update;
        UpdateChangelogText = update != null ? RichText.ToPlain(update.Changelog) : "";
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        IsCheckingUpdate = true;
        UpdateCheckMessage = null;
        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            ApplyBackgroundUpdateCheck(update);
            UpdateCheckMessage = update == null ? "У вас установлена последняя версия" : null;
        }
        catch
        {
            UpdateCheckMessage = "Не удалось проверить обновления — проверьте подключение к интернету";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (AvailableUpdate is not { } update) return;
        IsDownloadingUpdate = true;
        DownloadProgress = 0;
        try
        {
            var progress = new Progress<double>(p => DownloadProgress = p);
            var installerPath = await _updateService.DownloadAsync(update, progress);
            // Архив в "Загрузках" — показываем его в Finder: распаковать и перенести
            // приложение в "Программы" поверх старого.
            UpdateService.RevealInFinder(installerPath);
            UpdateCheckMessage = "Обновление скачано в «Загрузки» — распакуйте архив и перенесите Goji VPN в «Программы» с заменой.";
            AvailableUpdate = null;
            IsDownloadingUpdate = false;
        }
        catch
        {
            UpdateCheckMessage = "Не удалось скачать обновление — попробуйте ещё раз позже";
            IsDownloadingUpdate = false;
        }
    }
}
