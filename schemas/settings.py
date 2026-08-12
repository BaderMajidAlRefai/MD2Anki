from pydantic import BaseModel

class root_update(BaseModel):
    obsidian_root: str

class pattern_update(BaseModel):
    pattern_update: str

class anki_version_update(BaseModel):
    anki_version_update: int

class anki_url_update(BaseModel):
    anki_url_update: str

