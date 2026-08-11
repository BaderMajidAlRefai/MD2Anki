namespace ui.ViewModels;

public class CardReviewViewModel : ViewModelBase
{
    private bool _isSelected = true;

    public CardReviewViewModel(string front, string back)
    {
        Front = front;
        Back = back;
    }

    public string Front { get; }
    public string Back { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
