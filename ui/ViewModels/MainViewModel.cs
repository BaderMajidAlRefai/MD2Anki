using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ui.Models;

namespace ui.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string PlanUrl = "http://127.0.0.1:8000/plan";
    private const string ExecutePlanUrl = "http://127.0.0.1:8000/plan/execute";
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private Plan? _plan;
    private bool _isBusy;
    private bool _isReviewVisible;
    private string _statusMessage = string.Empty;

    public ObservableCollection<DeckReviewViewModel> Decks { get; } = [];

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

    public bool IsNotBusy => !IsBusy;

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

    [RelayCommand]
    private async Task LoadPlanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading sync plan…";

        try
        {
            using var response = await HttpClient.GetAsync(PlanUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            Plan = JsonSerializer.Deserialize<Plan>(json, JsonOptions)
                ?? throw new InvalidOperationException("The backend returned an empty plan.");

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
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Syncing…";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ExecutePlanUrl);
            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

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
        if (IsBusy || !IsReviewVisible)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Applying selected changes…";

        try
        {
            var adjustedPlan = BuildAdjustedPlan();
            using var response = await HttpClient.PostAsJsonAsync(
                ExecutePlanUrl,
                adjustedPlan,
                JsonOptions);
            response.EnsureSuccessStatusCode();

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
}
