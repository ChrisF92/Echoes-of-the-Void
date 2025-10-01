using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Systems;

namespace EchoesOfTheVoid.Core.Combat.Gambits {
  public class GambitRuntimeContext {
    public GambitRuntimeContext(Combatant actor, IReadOnlyList<ICombatant> allies, IReadOnlyList<ICombatant> enemies, int turnNumber, CombatSystem combatSystem, Random random = null) {
      Actor = actor;
      Allies = allies;
      Enemies = enemies;
      TurnNumber = turnNumber;
      CombatSystem = combatSystem;
      Random = random ?? new Random(Guid.NewGuid().GetHashCode());
    }

    public Combatant Actor { get; }
    public IReadOnlyList<ICombatant> Allies { get; }
    public IReadOnlyList<ICombatant> Enemies { get; }
    public int TurnNumber { get; }
    public CombatSystem CombatSystem { get; }
    public Random Random { get; }
  }
}
