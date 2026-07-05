import json
from parsing import extract_cards
from sync import *

with open("settings.json", "r") as settings_file:
    settings = json.load(settings_file)

print(get_obsidian_state(settings["obsidian"]["obsidian_root"], settings["obsidian"]["deck_type"]))
