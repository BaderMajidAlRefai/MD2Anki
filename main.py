import json, copy, argparse, uvicorn
from fastapi import FastAPI
from pathlib import Path
from platformdirs import user_config_dir

from schemas.plan import PlanSchema, DeckPlanSchema, CardPlanSchema
from schemas.settings import root_update, pattern_update, anki_version_update, anki_url_update

from models.AnkiConnectClient import AnkiConnectClient

from sync import (
    create_plan,
    get_anki_state,
    get_obsidian_state,
)

app = FastAPI()

CONFIG_DIRECTORY = Path(user_config_dir("MD2Anki"))
CONFIG_DIRECTORY.mkdir(parents=True, exist_ok=True)

SETTINGS_PATH = CONFIG_DIRECTORY / "settings.json"

DEFAULT_SETTINGS = {
    "anki": {
        "anki_connect_version": 6,
        "anki_connect_url": "http://127.0.0.1:8765"
    },
    "obsidian": {
        "obsidian_root": "",
        "notes_pattern": r"\s?-?\s?\*\*(.*?):\*\*\s(\S.*)"
    }
}

def save_settings():
    with open(SETTINGS_PATH, "w") as settings_file:
        json.dump(settings, settings_file, indent=4)

if SETTINGS_PATH.exists():
    with open(SETTINGS_PATH, 'r') as settings_file:
            settings = json.load(settings_file)
else:
    settings = copy.deepcopy(DEFAULT_SETTINGS)
    save_settings()

anki_client = AnkiConnectClient(
    settings["anki"]["anki_connect_version"],
    settings["anki"]["anki_connect_url"]
)
root_path = settings["obsidian"]["obsidian_root"]


        




@app.get("/plan")
def get_plan():
    plan = create_plan(get_anki_state(anki_client),get_obsidian_state(root_path, settings))
    return PlanSchema(
            deck_plan=DeckPlanSchema(
                to_be_added = plan.deck_plan.to_be_added
            ),
            card_plan=CardPlanSchema(
                to_be_added= plan.card_plan.to_be_added
            )
        )

@app.post("/plan/execute")
def post_plan(plan: PlanSchema | None = None):
    if plan is None:
        plan = create_plan(get_anki_state(anki_client),get_obsidian_state(root_path, settings))
    else:
        plan = plan.to_plan()
    plan.execute(anki_client)


@app.post("/settings/obsidian/root")
def change_root(update: root_update):
    global root_path
    root_path = update.obsidian_root
    settings["obsidian"]["obsidian_root"] = root_path
    save_settings()

    return {"obsidian_root": root_path}


@app.post("/settings/obsidian/notespattern")
def change_pattern(update: pattern_update):
    new_pattern = update.pattern_update
    settings["obsidian"]["notes_pattern"] = new_pattern
    save_settings()
    return {"notes_pattern": new_pattern}



@app.post("/settings/anki/version")
def change_anki_version(update: anki_version_update):
    new_version = update.anki_version_update
    settings["anki"]["anki_connect_version"] = new_version
    anki_client.version = new_version
    save_settings()

    return {"anki_connect_version": new_version}

@app.post("/settings/anki/url")
def change_anki_url(update: anki_url_update):
    new_url = update.anki_url_update
    settings["anki"]["anki_connect_url"] = new_url
    anki_client.url = new_url
    save_settings()

    return {"anki_connect_url": new_url}


@app.get("/settings")
def get_settings():
    return settings

@app.get("/health")
def health():
    return {"status": "ok"}

@app.get("/ankiclient/check")
def anki_check():
    connection = anki_client.check_connection()
    if connection:
        return True
    else:
        return False

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, required=True)
    args = parser.parse_args()

    uvicorn.run(
        app,
        host="127.0.0.1",
        port=args.port
    )


if __name__ == "__main__":
    main()
