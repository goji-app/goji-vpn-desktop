using System.IO;
using System.Text.Json;

namespace GodjiVpn.Services;

/// <summary>Сортировка списка серверов — порт Android ServerSort (SettingsRepository).</summary>
public enum ServerSort { Favorites, Ping, Name }

/// <summary>
/// Мелкие пользовательские настройки приложения, переживающие перезапуск (аналог
/// SettingsRepository в Android): %LOCALAPPDATA%\GodjiVpn\app-settings.json. Отдельные файлы
/// уже существующих настроек (тема, пинг, избранное) не переносятся сюда — чтобы не ломать
/// сохранённый выбор при обновлении.
/// </summary>
public sealed class AppSettings
{
    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "app-settings.json");

    public sealed class State
    {
        public ServerSort ServerSort { get; set; } = ServerSort.Favorites;
    }

    private readonly State _state;

    public event Action? Changed;

    public AppSettings() => _state = Load();

    public ServerSort ServerSort
    {
        get => _state.ServerSort;
        set { if (_state.ServerSort == value) return; _state.ServerSort = value; Save(); }
    }

    private static State Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new State();
            return JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new State();
        }
        catch { return new State(); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* лучшее усилие — при сбое записи настройка просто не переживёт перезапуск */ }
        Changed?.Invoke();
    }
}
