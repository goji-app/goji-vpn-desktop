using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodjiVpn.Models;
using GodjiVpn.Services;

namespace GodjiVpn.ViewModels;

public sealed partial class FaqItemUi : ObservableObject
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private bool isExpanded;

    public string ToggleGlyph => IsExpanded ? "−" : "+";

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

public sealed class FaqSectionUi
{
    public string? Name { get; init; }
    public required List<FaqItemUi> Items { get; init; }
    public bool HasName => !string.IsNullOrEmpty(Name);
}

/// <summary>Аналог FaqScreen.kt/FaqViewModel.kt — аккордеон вопрос/ответ, без пагинации. Порт
/// из Android (720f5ff).</summary>
public sealed partial class FaqViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool loading = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool loadError;

    public ObservableCollection<FaqSectionUi> Sections { get; } = new();
    public bool IsEmpty => !Loading && !LoadError && Sections.Count == 0;

    public event Action? BackRequested;

    public FaqViewModel(ApiClient api) => _api = api;

    public async Task LoadAsync()
    {
        Loading = true;
        LoadError = false;
        Sections.Clear();
        // Только для превью-экземпляра (GODJI_UI_PREVIEW_DEMO=1): вёрстка без API.
        if (Environment.GetEnvironmentVariable("GODJI_UI_PREVIEW_DEMO") == "1")
        {
            Sections.Add(new FaqSectionUi
            {
                Name = "Подключение",
                Items = new()
                {
                    new FaqItemUi { Question = "Почему VPN отключается через несколько минут?", Answer = "Проверьте, не включена ли экономия энергии для сетевого адаптера, и попробуйте другой узел.", IsExpanded = true },
                    new FaqItemUi { Question = "Какой узел выбрать?", Answer = "Автовыбор подключает к самому быстрому узлу по результатам пинга." }
                }
            });
            Loading = false;
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }
        try
        {
            var response = await _api.GetFaqAsync();
            var ungrouped = (response.Ungrouped ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.Question)).ToList();
            if (ungrouped.Count > 0)
                Sections.Add(new FaqSectionUi { Name = null, Items = ungrouped.Select(ToItem).ToList() });

            foreach (var section in (response.Sections ?? new()).Where(s => s.Items is { Count: > 0 }))
                Sections.Add(new FaqSectionUi { Name = section.Name, Items = section.Items!.Select(ToItem).ToList() });
        }
        catch { LoadError = true; }
        finally
        {
            Loading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    private static FaqItemUi ToItem(FaqItemDto i) => new() { Question = i.Question, Answer = i.Answer };
}
