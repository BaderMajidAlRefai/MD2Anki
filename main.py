from functions import *

with open("test.md", "r") as md:
    csv_conversion(extract_cards(md))