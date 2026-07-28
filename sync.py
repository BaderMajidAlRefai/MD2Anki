from os.path import isdir

import requests
import parsing
from pathlib import Path

def anki_connected(anki_port):
    try:
        requests.post(anki_port)
        return True
    except any:
        return False

def get_current_anki_decks(anki_port):
    current_anki_decks = requests.post(
        anki_port,
        json={
            "action" : "deckNamesAndIds",
            "version" : 6
        }
    )
    current_anki_decks = current_anki_decks.json()
    current_anki_decks = current_anki_decks["result"]

    return current_anki_decks

def setup_anki_state(current_anki_decks):
    anki_state = {}

    for deck_name, deck_id in current_anki_decks.items():
        anki_state[deck_name] = []

    return anki_state

def get_cards(anki_port, deck):
    cards = requests.post(
        anki_port, json={
            "action" : "findCards",
            "version" : 6,
            "params": {
                "query": f'deck:"{deck}"'
            }
        }
    )
    cards = cards.json()

    return cards

def get_cards_details(anki_port, cards):
    cards_details = requests.post(
        anki_port,json={
            "action" : "cardsInfo",
            "version" : 6,
            "params" : {
                "cards" : cards["result"]
            }
        }
    )
    cards_details = cards_details.json()
    cards_details = cards_details["result"]

    return cards_details

def add_cards_to_anki_state(anki_state, deck, cards_details):
    for details in cards_details:
        anki_state[deck].append(
            (details["fields"]["Front"], details["fields"]["Back"])
        )

def get_anki_state(anki_port):
    current_anki_decks = get_current_anki_decks(anki_port)
    anki_state = setup_anki_state(current_anki_decks)

    for deck in anki_state:
        cards = get_cards(anki_port, deck)
        cards_details = get_cards_details(anki_port, cards)
        add_cards_to_anki_state(anki_state, deck, cards_details)
    
    return anki_state

def get_obsidian_state(root_path, settings):
    obsidian_state = {}
    obsidian_root = Path(root_path).expanduser()

    for sub_directory in obsidian_root.iterdir():
        if sub_directory.is_dir():
            obsidian_state[sub_directory.name] = []
            for file in sub_directory.iterdir():
                if file.is_file():
                    with open(file, 'r') as md:
                        extracted_cards = parsing.extract_cards(md, settings)
                        obsidian_state[sub_directory.name].extend(extracted_cards)

    return obsidian_state

def create_plan(anki_state, obsidian_state):
    deck_plan = comparing_decks(anki_state,obsidian_state)
    card_plan = comparing_cards(anki_state,obsidian_state)
    master_plan = {}
    master_plan["deck_plan"] = deck_plan
    master_plan["card_plan"] = card_plan
    return master_plan

def comparing_decks(anki_state, obsidian_state):
    deck_plan = {}

    existing_decks = [deck for deck in obsidian_state if deck in anki_state]
    new_decks = [deck for deck in obsidian_state if deck not in anki_state]

    deck_plan["existing_decks"] = existing_decks
    deck_plan["new_decks"] = new_decks
    return deck_plan

def comparing_cards(anki_state,obsidian_state):
    card_plan = {}
    
    for obsidian_deck in obsidian_state:
        if anki_state.get(obsidian_deck):
            anki_cards = anki_state.get(obsidian_deck)
        else:
            anki_cards =  []
        obsidian_cards = obsidian_state[obsidian_deck]

        new_cards = [card for card in obsidian_cards if card not in anki_cards]
        to_be_deleted = [card for card in anki_cards if card not in obsidian_cards]

        card_plan[obsidian_deck] = {"new_cards" : new_cards, "to_be_deleted" : to_be_deleted}
    return card_plan

def display_plan(plan):
    text = ""
    if plan["deck_plan"]["new_decks"]:
        text += f"Decks to be added: {len(plan['deck_plan']['new_decks'])}.\n"
        for deck in plan["deck_plan"]["new_decks"]:
            text += f"{deck} \n"
    text += f"Existing Decks: {len(plan['deck_plan']['existing_decks'])}\n"
    for deck in plan["deck_plan"]["existing_decks"]:
        text += f"{deck} \n"
    for deck in plan["card_plan"]:
        text += f"Cards being added to {deck}: {len(plan['card_plan'][deck]['new_cards'])}\n"
        for card in plan["card_plan"][deck]["new_cards"]:
            text += f"{card[0]}: {card[1]} \n"
        text += f"Cards being deleted from {deck}: {len(plan['card_plan'][deck]['to_be_deleted'])}\n"
        for card in plan["card_plan"][deck]['to_be_deleted']:
            text += f"{card[0]}: {card[1]}\n"
    return text

def execute_plan(plan, anki_port):
    for deck in plan["deck_plan"]["new_decks"]:
        requests.post(anki_port,
                json={
                    "action" : "createDeck",
                    "version" : 6,
                    "params" : {"deck" : deck}
                      })
    for deck in plan["card_plan"]:
        for front, back in plan["card_plan"][deck]["new_cards"]:
            requests.post(anki_port,
                          json={
                              "action" : "addNote",
                              "version" : 6,
                              "params" : {
                                  "note" : {
                                      "deckName" : deck,
                                      "modelName" : "basic",
                                      "fields" : {
                                          "Front" : front,
                                          "Back" : back
                                          }
                                      }
                                  }
                              })


