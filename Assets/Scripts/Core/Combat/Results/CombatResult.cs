using System.Collections.Generic;

using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Results {
  public class CombatResult {
    public CombatOutcome Outcome { get; }
    public List<ICombatant> Survivors { get; }
    public float CombatDuration { get; set; }
    public int TotalRounds { get; set; }

    public CombatResult(CombatOutcome outcome, List<ICombatant> survivors) {
      Outcome = outcome;
      Survivors = survivors ?? new List<ICombatant>();
    }
  }
}

