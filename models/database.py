from sqlalchemy import create_engine
from sqlalchemy.orm import DeclarativeBase

engine = create_engine("sqlite:///db.db")


class Base(DeclarativeBase):
    pass