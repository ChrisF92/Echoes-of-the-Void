using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Data;

namespace EchoesOfTheVoid.Core.Roster.Progression.Contracts {
  public interface IStatProgression {
    int EvaluateStat(StatType statType, int level, int baseValue);
    CombatStats BuildStatSnapshot(CombatStats baseStats, int level);
  }
}
