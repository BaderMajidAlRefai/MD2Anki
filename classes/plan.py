class DeckPlan:
    def __init__(self, to_be_added: list | None, to_be_deleted: list | None):
        self.to_be_added = to_be_added
        self.to_be_deleted = to_be_deleted

    def adding_decks(self, anki_client):
        for deck in self.to_be_added:
            anki_client.add_deck(deck)

class CardPlan:
    def __init__(self, to_be_added, to_be_deleted):
        self.to_be_added = to_be_added
        self.to_be_deleted = to_be_deleted

    def adding_cards(self, anki_client):
        for deck_name in self.to_be_added:
            for front, back in self.to_be_added[deck_name]:
                anki_client.add_card_to_deck(deck_name, front, back)

class Plan:
    def __init__(self, deck_plan : DeckPlan, card_plan : CardPlan):
        self.deck_plan = deck_plan
        self.card_plan = card_plan

    def __str__(self):
        text = f"""
        Decks:\nTo be added:
        {self.deck_plan.to_be_added}
        To be deleted:
        {self.deck_plan.to_be_deleted}
        Cards:\nTo be added:
        {self.card_plan.to_be_added}
        To be deleted:
        {self.card_plan.to_be_deleted}
        """
        return text

    def execute(self, anki_client):
        self.deck_plan.adding_decks(anki_client)
        self.card_plan.adding_cards(anki_client)
