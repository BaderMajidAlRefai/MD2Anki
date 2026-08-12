using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ui.Models;
using ui.Services;

namespace ui.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private Plan? _plan;
    private bool _hasConnectionResults;
    private bool _hasObsidianRoot;
    private bool _isAnkiConnected;
    private bool _isBackendReady;
    private bool _isBusy;
    private bool _isCheckingConnections;
    private bool _isReviewVisible;
    private string _backendStatusLabel = "Starting";
    private string _statusMessage = string.Empty;

    public ObservableCollection<DeckReviewViewModel> Decks { get; } = [];

    public MainViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        StatusMessage = "Starting local backend…";
    }

    public Plan? Plan
    {
        get => _plan;
        private set => SetProperty(ref _plan, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsBackendReady
    {
        get => _isBackendReady;
        private set
        {
            if (SetProperty(ref _isBackendReady, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy && IsBackendReady && AreConnectionsReady;

    public bool HasObsidianRoot => _hasObsidianRoot;

    public bool IsObsidianRootMissing => _hasConnectionResults && !HasObsidianRoot;

    public bool IsAnkiConnected => _isAnkiConnected;

    public bool IsAnkiDisconnected => _hasConnectionResults && !IsAnkiConnected;

    public bool IsConnectionStatusNeutral => !_hasConnectionResults;

    public bool AreConnectionsReady =>
        _hasConnectionResults && HasObsidianRoot && IsAnkiConnected;

    public bool DoConnectionsNeedAttention =>
        _hasConnectionResults && !AreConnectionsReady;

    public string NeutralConnectionStatusLabel =>
        _isCheckingConnections ? "Checking…" : "Unavailable";

    public string BackendStatusLabel
    {
        get => _backendStatusLabel;
        private set => SetProperty(ref _backendStatusLabel, value);
    }

    public bool IsReviewVisible
    {
        get => _isReviewVisible;
        private set => SetProperty(ref _isReviewVisible, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public void SetBackendReady()
    {
        IsBackendReady = true;
        BackendStatusLabel = "Checking";
        StatusMessage = string.Empty;
        _ = RefreshConnectionStatusAsync();
    }

    public void SetBackendError(string message)
    {
        IsBackendReady = false;
        IsBusy = false;
        BackendStatusLabel = "Unavailable";
        StatusMessage = message;
        SetConnectionUnavailable();
    }

    public async Task RefreshConnectionStatusAsync()
    {
        if (!IsBackendReady || _isCheckingConnections)
        {
            return;
        }

        SetConnectionChecking();

        try
        {
            var settingsTask = _apiClient.GetFromJsonAsync<ApplicationSettings>("settings");
            var ankiCheckTask = _apiClient.GetFromJsonAsync<bool>("ankiclient/check");

            await Task.WhenAll(settingsTask, ankiCheckTask);

            var applicationSettings = await settingsTask;
            var hasObsidianRoot = !string.IsNullOrWhiteSpace(
                applicationSettings.Obsidian.ObsidianRoot);
            var isAnkiConnected = await ankiCheckTask;

            SetConnectionResults(hasObsidianRoot, isAnkiConnected);
        }
        catch (Exception exception)
        {
            SetConnectionUnavailable();
            BackendStatusLabel = "Unavailable";
            StatusMessage = $"Could not check connections: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckConnectionsAsync()
    {
        await RefreshConnectionStatusAsync();
    }

    [RelayCommand]
    private async Task LoadPlanAsync()
    {
        if (!IsNotBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading sync plan…";

        try
        {
            Plan = await _apiClient.GetFromJsonAsync<Plan>("plan");

            PopulateDecks(Plan);
            IsReviewVisible = Decks.Count > 0;
            StatusMessage = Decks.Count == 0
                ? "Anki is up to date."
                : "Review the additions below.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not load the plan: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task QuickSyncAsync()
    {
        if (!IsNotBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Syncing…";

        try
        {
            await _apiClient.PostAsync("plan/execute");

            IsReviewVisible = false;
            Decks.Clear();
            Plan = null;
            StatusMessage = "Quick sync complete.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Quick sync failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExecutePlanAsync()
    {
        if (!IsNotBusy || !IsReviewVisible)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Applying selected changes…";

        try
        {
            var adjustedPlan = BuildAdjustedPlan();
            await _apiClient.PostAsJsonAsync("plan/execute", adjustedPlan);

            IsReviewVisible = false;
            Decks.Clear();
            Plan = null;
            StatusMessage = "Selected changes synced successfully.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Could not execute the plan: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateDecks(Plan plan)
    {
        Decks.Clear();

        var newDecks = plan.DeckPlan.ToBeAdded.ToHashSet();
        var deckNames = newDecks
            .Concat(plan.CardPlan.ToBeAdded.Keys)
            .Distinct()
            .OrderBy(deckName => deckName);

        foreach (var deckName in deckNames)
        {
            plan.CardPlan.ToBeAdded.TryGetValue(deckName, out var cards);
            var isNewDeck = newDecks.Contains(deckName);

            if (!isNewDeck && (cards is null || cards.Count == 0))
            {
                continue;
            }

            Decks.Add(new DeckReviewViewModel(
                deckName,
                isNewDeck,
                cards ?? []));
        }
    }

    private Plan BuildAdjustedPlan()
    {
        var decksToAdd = Decks
            .Where(deck => deck.IsNewDeck && deck.IsSelected)
            .Select(deck => deck.Name)
            .ToList();

        var cardsToAdd = Decks
            .Where(deck => deck.IsSelected)
            .Select(deck => new
            {
                deck.Name,
                Cards = deck.Cards
                    .Where(card => card.IsSelected)
                    .Select(card => new List<string> { card.Front, card.Back })
                    .ToList(),
            })
            .Where(deck => deck.Cards.Count > 0)
            .ToDictionary(deck => deck.Name, deck => deck.Cards);

        return new Plan
        {
            DeckPlan = new DeckPlan { ToBeAdded = decksToAdd },
            CardPlan = new CardPlan { ToBeAdded = cardsToAdd },
        };
    }

    private void SetConnectionChecking()
    {
        _isCheckingConnections = true;
        _hasConnectionResults = false;
        _hasObsidianRoot = false;
        _isAnkiConnected = false;
        BackendStatusLabel = "Checking";
        NotifyConnectionStateChanged();
    }

    private void SetConnectionResults(bool hasObsidianRoot, bool isAnkiConnected)
    {
        _isCheckingConnections = false;
        _hasConnectionResults = true;
        _hasObsidianRoot = hasObsidianRoot;
        _isAnkiConnected = isAnkiConnected;
        BackendStatusLabel = AreConnectionsReady ? "Ready" : "Action needed";
        NotifyConnectionStateChanged();
    }

    private void SetConnectionUnavailable()
    {
        _isCheckingConnections = false;
        _hasConnectionResults = false;
        _hasObsidianRoot = false;
        _isAnkiConnected = false;
        NotifyConnectionStateChanged();
    }

    private void NotifyConnectionStateChanged()
    {
        OnPropertyChanged(nameof(HasObsidianRoot));
        OnPropertyChanged(nameof(IsObsidianRootMissing));
        OnPropertyChanged(nameof(IsAnkiConnected));
        OnPropertyChanged(nameof(IsAnkiDisconnected));
        OnPropertyChanged(nameof(IsConnectionStatusNeutral));
        OnPropertyChanged(nameof(AreConnectionsReady));
        OnPropertyChanged(nameof(DoConnectionsNeedAttention));
        OnPropertyChanged(nameof(NeutralConnectionStatusLabel));
        OnPropertyChanged(nameof(IsNotBusy));
    }
}
