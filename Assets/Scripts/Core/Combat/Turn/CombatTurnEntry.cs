using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Turn
{
  public class CombatTurnEntry
  {
    public ICombatant Combatant { get; }
    public float InitiativeBonus { get; set; } = 0f;

    public CombatTurnEntry(ICombatant combatant)
    {
      Combatant = combatant;
    }
  }
}

