import json
from functions import csv_conversion, extract_cards

with open("settings.json", "r") as settings_file:
    settings = json.load(settings_file)

print (settings)
with open("test.md", "r") as md:
    csv_conversion(extract_cards(md))