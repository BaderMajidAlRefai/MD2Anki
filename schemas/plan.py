from pydantic import BaseModel
from models.plan import Plan, DeckPlan, CardPlan

class DeckPlanSchema(BaseModel):
    to_be_added: list[str] | None

class CardPlanSchema(BaseModel):
    to_be_added: dict[str, list[tuple[str, str]]] | None

class PlanSchema(BaseModel):
    deck_plan: DeckPlanSchema
    card_plan: CardPlanSchema 

    def to_plan(self):
        return Plan(
            deck_plan=DeckPlan(
                to_be_added=self.deck_plan.to_be_added
            ),
            card_plan=CardPlan(
                to_be_added=self.card_plan.to_be_added
            )
        )