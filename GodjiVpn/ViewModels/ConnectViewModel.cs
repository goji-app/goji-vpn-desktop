using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodjiVpn.Services;
using GodjiVpn.Utils;

namespace GodjiVpn.ViewModels;

/// <summary>Аналог ConnectViewModel.kt/ConnectScreen.kt (Android) — заголовок/статус-строка,
/// приветствие поверх глобуса, карточка текущего узла, скорость/трафик. Авто-переключение на
/// резервный узел при глушении мобильной сети сознательно не переносится — специфично для SIM,
/// на десктопе такого сценария нет (решение принято с пользователем). Плашка сети справа в шапке
/// показывает тип активного подключения компьютера (Wi-Fi/Ethernet) вместо Wi-Fi/Мобильная.</summary>
public sealed partial class ConnectViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private readonly VpnEngine _vpnEngine;
    private readonly SubscriptionRepository _subscription;
    private readonly DispatcherTimer _speedTimer;

    private long _lastRx;
    private long _lastTx;
    private DateTime _lastSampleUtc;

    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isConnecting;
    [ObservableProperty] private string? errorMessage;

    [ObservableProperty] private string headline = "Пока без защиты";
    [ObservableProperty] private string subline = "Провайдер видит всё, что ты открываешь";

    [ObservableProperty] private string currentNodeName = "Выбрать за меня";
    [ObservableProperty] private string currentNodeMeta = "";
    [ObservableProperty] private string currentFlag = "🌐";
    [ObservableProperty] private string? currentFlagImagePath;

    [ObservableProperty] private string greetingHi = "Hello";
    [ObservableProperty] private string greetingSub = "«хелло» · английский";

    [ObservableProperty] private string globeStatus = "off";
    [ObservableProperty] private CountryGeo? globeGeo;

    [ObservableProperty] private string planName = "—";
    [ObservableProperty] private string expiryLabel = "—";
    [ObservableProperty] private int daysLeft;
    [ObservableProperty] private double usedGb;
    [ObservableProperty] private double quotaGb;
    [ObservableProperty] private bool isUnlimited;

    [ObservableProperty] private string trafficValueLabel = "0,0 ГБ / ∞";
    [ObservableProperty] private string trafficExpiryLabel = "";
    [ObservableProperty] private double trafficFraction;
    [ObservableProperty] private bool showTrafficBar;

    [ObservableProperty] private string downSpeedLabel = "0,0";
    [ObservableProperty] private string upSpeedLabel = "0,0";
    [ObservableProperty] private string connectedTimeLabel = "00:00:00";

    /// <summary>Тип активного подключения компьютера — плашка справа в шапке (NetworkPill).</summary>
    [ObservableProperty] private string netLabel = "Сеть";
    [ObservableProperty] private bool hasNetwork = true;

    public event Action? NavigateToPlansRequested;
    public event Action? NavigateToServersRequested;

    [RelayCommand]
    private void OpenTraffic() => NavigateToPlansRequested?.Invoke();

    /// <summary>Быстрая смена узла с Главной (Android 38457d4): строка узла открывает "Серверы".</summary>
    [RelayCommand]
    private void OpenServers() => NavigateToServersRequested?.Invoke();

    public ConnectViewModel(VpnEngine vpnEngine, SubscriptionRepository subscription)
    {
        _vpnEngine = vpnEngine;
        _subscription = subscription;

        _vpnEngine.PropertyChanged += OnVpnEnginePropertyChanged;
        _subscription.PropertyChanged += OnSubscriptionPropertyChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _speedTimer.Tick += (_, _) => SampleSpeed();

        RefreshFromState();
        RefreshNetwork();
    }

    public async Task LoadAsync()
    {
        await _subscription.RefreshAsync();
        RefreshFromState();
    }

    private bool CanToggle => !IsConnecting;

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private async Task ToggleAsync()
    {
        ErrorMessage = null;
        if (IsConnected)
        {
            await _vpnEngine.DisconnectAsync();
            return;
        }

        var node = _subscription.SelectedNode;
        if (node == null)
        {
            ErrorMessage = "Нет доступного сервера — выберите его на вкладке «Серверы»";
            return;
        }
        await _vpnEngine.ConnectAsync(node);
    }

    private void OnVpnEnginePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RunOnUiThread(RefreshFromState);

    private void OnSubscriptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RunOnUiThread(RefreshFromState);

    private void OnNetworkChanged(object? sender, EventArgs e) => RunOnUiThread(RefreshNetwork);

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => RunOnUiThread(RefreshNetwork);

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    /// <summary>Физическое подключение с маршрутом по умолчанию, не считая самого туннеля (Wintun)
    /// и прочих виртуальных адаптеров.</summary>
    private void RefreshNetwork()
    {
        try
        {
            var active = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                            && !n.Description.Contains("Wintun", StringComparison.OrdinalIgnoreCase)
                            && !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                            && !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                            && n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                                                           && !g.Address.Equals(System.Net.IPAddress.Any)))
                .ToList();
            var wifi = active.FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            if (wifi != null) { NetLabel = "Wi-Fi"; HasNetwork = true; }
            else if (active.Count > 0) { NetLabel = "Ethernet"; HasNetwork = true; }
            else { NetLabel = "Нет сети"; HasNetwork = false; }
        }
        catch
        {
            NetLabel = "Сеть";
            HasNetwork = true;
        }
    }

    private void RefreshFromState()
    {
        IsConnected = _vpnEngine.IsRunning;
        IsConnecting = _vpnEngine.IsConnecting;
        ErrorMessage = _vpnEngine.LastError ?? _subscription.LastError;
        ToggleCommand.NotifyCanExecuteChanged();

        var node = _subscription.SelectedNode;
        var geo = node != null ? CountryGeoLookup.Find(node.Name) : null;
        GlobeGeo = geo;

        CurrentNodeName = node != null ? RemarkText.StripLeadingFlag(node.Name) : "Выбрать за меня";
        CurrentNodeMeta = geo != null ? $"{geo.City}, {geo.RuName}" : "";
        CurrentFlag = geo != null ? CountryGeoLookup.FlagEmoji(geo.Code) : "🌐";
        CurrentFlagImagePath = FlagIcon.ImagePath(geo?.Code);

        var greeting = Greetings.ForLang(geo?.Lang);
        GreetingHi = greeting.Hi;
        GreetingSub = (greeting.Transliteration.Length > 0 ? $"«{greeting.Transliteration}» · " : "") + greeting.Language;

        GlobeStatus = IsConnected ? "on" : IsConnecting ? "connecting" : "off";

        var ruPrep = geo?.RuPrep ?? "надёжном месте";
        var nodeLabel = node != null ? RemarkText.StripLeadingFlag(node.Name) : "Выбрать за меня";
        Headline = IsConnected ? $"Ты в {ruPrep}" : IsConnecting ? "Ищем дорогу…" : "Пока без защиты";
        Subline = IsConnected ? $"{nodeLabel} · в туннеле"
            : IsConnecting ? $"Договариваемся с {nodeLabel}"
            : "Провайдер видит всё, что ты открываешь";

        var sub = _subscription.Subscription;
        PlanName = sub?.PlanName ?? "—";
        ExpiryLabel = sub?.ExpireAt is { } iso ? DateFormat.FormatDate(iso) : "—";
        DaysLeft = sub?.DaysLeft ?? 0;
        UsedGb = (sub?.Traffic?.UsedBytes ?? 0) / 1_000_000_000.0;
        QuotaGb = (sub?.Traffic?.LimitBytes ?? 0) / 1_000_000_000.0;
        IsUnlimited = sub?.Traffic?.IsUnlimited ?? false;

        var unlimited = IsUnlimited || QuotaGb <= 0;
        var used = UsedGb.ToString("0.0", Ru);
        TrafficValueLabel = unlimited ? $"{used} ГБ / ∞" : $"{used} / {(int)QuotaGb} ГБ";
        TrafficFraction = unlimited ? 0 : Math.Clamp(UsedGb / QuotaGb, 0, 1);
        ShowTrafficBar = !unlimited;
        TrafficExpiryLabel = $"Подписка до {ExpiryLabel} · осталось {DaysLeft} {RuPlural.Days(DaysLeft)}";

        if (IsConnected)
        {
            var counters = _vpnEngine.ReadAdapterCounters();
            _lastRx = counters?.RxBytes ?? 0;
            _lastTx = counters?.TxBytes ?? 0;
            _lastSampleUtc = DateTime.UtcNow;
            if (!_speedTimer.IsEnabled) _speedTimer.Start();
        }
        else
        {
            _speedTimer.Stop();
            DownSpeedLabel = "0,0";
            UpSpeedLabel = "0,0";
            ConnectedTimeLabel = "00:00:00";
        }
    }

    private void SampleSpeed()
    {
        var counters = _vpnEngine.ReadAdapterCounters();
        var now = DateTime.UtcNow;
        if (counters != null)
        {
            var dt = Math.Max((now - _lastSampleUtc).TotalSeconds, 0.001);
            var down = Math.Max(counters.Value.RxBytes - _lastRx, 0) / dt / 1_000_000.0;
            var up = Math.Max(counters.Value.TxBytes - _lastTx, 0) / dt / 1_000_000.0;
            DownSpeedLabel = down.ToString("0.0", Ru);
            UpSpeedLabel = up.ToString("0.0", Ru);
            _lastRx = counters.Value.RxBytes;
            _lastTx = counters.Value.TxBytes;
        }
        _lastSampleUtc = now;

        var since = _vpnEngine.ConnectedSinceUtc;
        if (since != null)
        {
            var elapsed = now - since.Value;
            ConnectedTimeLabel = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }
    }

    public void Dispose()
    {
        _speedTimer.Stop();
        _vpnEngine.PropertyChanged -= OnVpnEnginePropertyChanged;
        _subscription.PropertyChanged -= OnSubscriptionPropertyChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }
}
