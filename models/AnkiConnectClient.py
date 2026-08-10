import requests


class AnkiConnectClient:
    def __init__(self, version, url="http://localhost:8765"):
        self.version = version
        self.url = url

    def check_connection(self):
        try:
            requests.post(self.url, timeout=5)
            return True
        except requests.RequestException:
            return False

    def send_request(self, action, params=None):
        request = {
            "action": action,
            "version": self.version
        }

        if params:
            request["params"] = params

        response = requests.post(self.url, json=request, timeout=10)
        response.raise_for_status()
        response = response.json()

        if response.get("error"):
            raise RuntimeError(response["error"])

        return response["result"]

    def get_current_decks(self):
        return self.send_request("deckNamesAndIds")

    def add_deck(self, deck_name):
        return self.send_request("createDeck", {"deck": deck_name})

    def get_decks_cards(self, deck):
        return self.send_request("findCards", {"query": f'deck:"{deck}"'})

    def get_card_details(self, cards):
        return self.send_request("cardsInfo", {"cards": cards})

    def add_card_to_deck(self, deck_name, front, back):
        note = {
            "deckName": deck_name,
            "modelName": "Basic",
            "fields": {
                "Front": front,
                "Back": back
            }
        }
        return self.send_request("addNote", {"note": note})
