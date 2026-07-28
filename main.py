import json

from parsing import extract_cards
from sync import (
    anki_connected,
    create_plan,
    display_plan,
    execute_plan,
    get_anki_state,
    get_obsidian_state,
)

def main():
    with open("settings.json", "r") as settings_file:
        settings = json.load(settings_file)

    anki_port = settings["anki"]["anki_connect_port"]
    obsidian_root = settings["obsidian"]["obsidian_root"]

    if anki_connected() == 0:
        return "Failed. Please ensure you have Anki open or have the Anki connect extension installed."
    else:
        0

    anki_state = get_anki_state(anki_port)
    obsidian_state = get_obsidian_state(
        obsidian_root,
        settings
    )

    plan = create_plan(
        anki_state,
        obsidian_state
    )

    print(display_plan(plan))

    execute_plan(plan, anki_port)


if __name__ == "__main__":
    main()