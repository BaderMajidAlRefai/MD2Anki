import re

def extract_cards(md):
    pattern = r"\s?-?\s?\*\*(.*?):\*\*\s(.*)"
    questions = []

    for line in md:
        extracted_line = re.match(pattern, line)
        if extracted_line:
            questions.append(extracted_line.groups())

    return questions

def csv_conversion(cards_array):
    with open('cards.csv', 'w') as file:
        for question, answer in cards_array:
            file.write(f"{question}, {answer}\n")
            
    return
