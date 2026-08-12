using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ui.ViewModels;

public class DeckReviewViewModel : ViewModelBase
{
    private bool _isSelected = true;
    private bool _isExpanded = true;

    public DeckReviewViewModel(
        string name,
        bool isNewDeck,
        IEnumerable<List<string>> cards)
    {
        Name = name;
        IsNewDeck = isNewDeck;
        Cards = new ObservableCollection<CardReviewViewModel>(
            cards.Select(card => new CardReviewViewModel(
                card.ElementAtOrDefault(0) ?? string.Empty,
                card.ElementAtOrDefault(1) ?? string.Empty)));
    }

    public string Name { get; }
    public bool IsNewDeck { get; }
    public ObservableCollection<CardReviewViewModel> Cards { get; }
    public bool HasNoCards => Cards.Count == 0;
    public string CardCountLabel => Cards.Count == 1 ? "1 card" : $"{Cards.Count} cards";
    public string Subtitle => IsNewDeck
        ? $"New deck · {CardCountLabel}"
        : $"{CardCountLabel} to add";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            foreach (var card in Cards)
            {
                card.IsSelected = value;
            }
        }
    }
}
