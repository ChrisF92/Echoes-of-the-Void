using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Run {
  /// <summary>
  /// Lightweight snapshot of a combatant's state for reporting run results.
  /// </summary>
  public sealed class CombatRunCombatantSnapshot {
    public CombatRunCombatantSnapshot(Combatant source) {
      if (source != null) {
        InstanceId = source.GetInstanceID();
        Name = source.Name;
        IsPlayerControlled = source.IsPlayerControlled;
        CurrentHealth = source.GetStat(StatType.Health);
        MaxHealth = source.GetMaxStat(StatType.Health);
        IsAlive = source.IsAlive;
      } else {
        InstanceId = 0;
        Name = string.Empty;
        IsPlayerControlled = true;
        CurrentHealth = 0;
        MaxHealth = 0;
        IsAlive = false;
      }
    }

    public int InstanceId { get; }
    public string Name { get; }
    public bool IsPlayerControlled { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }
    public bool IsAlive { get; }
    public float HealthRatio => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;
  }
}
