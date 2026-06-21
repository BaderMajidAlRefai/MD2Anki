import re

def extract_cards(md):
    pattern = "\*\*(.*?)\*\*:\s(.*)"
    questions = []

    for line in md:
        extracted_line = re.match(pattern, line)
        if extracted_line:
            questions.append(extracted_line.groups())

    return questions

with open("test.md", "r") as md:
    print(extract_cards(md))

def csv_conversion(cards_dict):
    return
