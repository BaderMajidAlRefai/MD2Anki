import json

from parsing import extract_cards
from sync import (
    create_plan,
    execute_plan,
    get_anki_state,
    get_obsidian_state,
)

from models.AnkiConnectClient import AnkiConnectClient

def main():
    with open('settings.json', 'r') as settings_file:
            settings = json.load(settings_file)
            anki_client = AnkiConnectClient(
                     settings["anki"]["anki_connect_version"],
                     settings["anki"]["anki_connect_url"]
                )

            root_path = settings["obsidian"]["obsidian_root"]

    plan = create_plan(get_anki_state(anki_client), get_obsidian_state(root_path, settings))
    plan.execute(anki_client)
if __name__ == "__main__":
    main()