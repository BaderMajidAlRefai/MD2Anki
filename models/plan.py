class DeckPlan:
    def __init__(self, to_be_added: list | None):
        self.to_be_added = to_be_added

    def adding_decks(self, anki_client):
        for deck in self.to_be_added:
            anki_client.add_deck(deck)

class CardPlan:
    def __init__(self, to_be_added):
        self.to_be_added = to_be_added

    def adding_cards(self, anki_client):
        return anki_client.add_notes(self.to_be_added)

class Plan:
    def __init__(self, deck_plan : DeckPlan, card_plan : CardPlan):
        self.deck_plan = deck_plan
        self.card_plan = card_plan

    def __str__(self):
        text = f"""
        Decks:\nTo be added:
        {self.deck_plan.to_be_added}
        Cards:\nTo be added:
        {self.card_plan.to_be_added}
        """
        return text

    def execute(self, anki_client):
        self.deck_plan.adding_decks(anki_client)
        self.card_plan.adding_cards(anki_client)
