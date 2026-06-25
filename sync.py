import requests
def sync_to_anki(anki_port):
    if error := check_anki_connect(anki_port):
        return error
    anki_state = get_anki_state(anki_port)
    print(anki_state)

def check_anki_connect(anki_port):
    try:
        requests.post(anki_port)
        return None
    except Exception:
        return ("""ERROR: Anki connect is not found or not responding.
                      \nPotential Issues: 
                      \nAnki is not open. 
                      \nYou do not have anki connect installed. 
                      \nSomething is wrong with anki connect.""")

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
        anki_state[deck_name] = {
            "id" : deck_id,
            "cards" : {}
        }

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
        card_id = details["cardId"]
        anki_state[deck]["cards"][card_id] = {
            "front_side" : details["fields"]["Front"],
            "back_side" : details["fields"]["Back"]
        }

def get_anki_state(anki_port):
    current_anki_decks = get_current_anki_decks(anki_port)
    anki_state = setup_anki_state(current_anki_decks)

    for deck in anki_state:
        cards = get_cards(anki_port, deck)
        cards_details = get_cards_details(anki_port, cards)
        add_cards_to_anki_state(anki_state, deck, cards_details)
        
    return anki_state

def get_obsidian_state():
    return
