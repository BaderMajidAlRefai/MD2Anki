from functions import csv_conversion, extract_cards

with open("test.md", "r") as md:
    csv_conversion(extract_cards(md))