import parsing
from pathlib import Path

from models.plan import CardPlan, DeckPlan, Plan


def get_current_anki_decks(anki_client):
    return anki_client.get_current_decks()

def setup_anki_state(current_anki_decks):
    anki_state = {}
    for deck_name, deck_id in current_anki_decks.items():
        anki_state[deck_name] = []

    return anki_state

def get_cards_details(anki_client, cards):
    return anki_client.get_card_details(cards)

def add_cards_to_anki_state(anki_state, deck, cards_details):
    for details in cards_details:
        anki_state[deck].append(
            (details["fields"]["Front"]["value"], details["fields"]["Back"]["value"])
        )

def get_anki_state(anki_client):
    current_anki_decks = get_current_anki_decks(anki_client)
    anki_state = setup_anki_state(current_anki_decks)

    for deck in anki_state:
        cards = anki_client.get_decks_cards(deck)
        cards_details = get_cards_details(anki_client, cards)
        add_cards_to_anki_state(anki_state, deck, cards_details)
    
    return anki_state

def get_obsidian_state(root_path, settings):
    obsidian_state = {}
    obsidian_root = Path(root_path).expanduser()

    for sub_directory in obsidian_root.iterdir():
        if sub_directory.is_dir():
            obsidian_state[sub_directory.name] = []
            for file in sub_directory.iterdir():
                if file.is_file() and file.suffix == ".md":
                    with open(file, "r", encoding="utf-8") as md:
                        extracted_cards = parsing.extract_cards(md, settings)
                        obsidian_state[sub_directory.name].extend(extracted_cards)
    return obsidian_state

def create_plan(anki_state, obsidian_state):
    deck_plan = comparing_decks(anki_state,obsidian_state)
    card_plan = comparing_cards(anki_state,obsidian_state)
    plan = Plan(deck_plan, card_plan)
    return plan

def comparing_decks(anki_state, obsidian_state):
    new_decks = [deck for deck in obsidian_state if deck not in anki_state]
    deck_plan = DeckPlan(new_decks)
    return deck_plan

def comparing_cards(anki_state,obsidian_state):
    new_cards = {}
    
    for obsidian_deck in obsidian_state:
        if anki_state.get(obsidian_deck):
            anki_cards = anki_state.get(obsidian_deck)
        else:
            anki_cards =  []
        obsidian_cards = obsidian_state[obsidian_deck]

        new_cards[obsidian_deck] = [card for card in obsidian_cards if card not in anki_cards]

    card_plan = CardPlan(new_cards)
    return card_plan


