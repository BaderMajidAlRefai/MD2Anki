using System.Collections.Generic;
namespace ui.Models;

public class Plan
{
    public DeckPlan DeckPlan {get; set;} = new();
    public CardPlan CardPlan {get; set;} = new();
}

public class DeckPlan
{
    public List<string> ToBeAdded {get; set;} = new();
}

public class CardPlan
{
    public Dictionary<string, List<List<string>>> ToBeAdded { get; set; } = new();
}