from sqlalchemy import String
from sqlalchemy.orm import Mapped, mapped_column
from database import Base

class Card(Base):
    __tablename__ = "cards"
    id: Mapped[int] = mapped_column(primary_key=True)
    anki_note_id: Mapped[int | None] = mapped_column(unique=True)
    obsidian_location: Mapped[str]
    concept: Mapped[str]
    answer: Mapped[str]
    