import json
from parsing import extract_cards
from sync import *

with open("settings.json", "r") as settings_file:
    settings = json.load(settings_file)

print(check_anki_connect(settings["anki"]["anki_connect_port"]))
plan = create_plan(
        get_anki_state(settings["anki"]["anki_connect_port"]),
        get_obsidian_state(settings["obsidian"]["obsidian_root"], settings)
        )
print(display_plan(plan))



execute_plan(plan, settings["anki"]["anki_connect_port"])


