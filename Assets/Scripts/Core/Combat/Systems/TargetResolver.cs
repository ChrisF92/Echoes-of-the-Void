using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Resolves valid targets based on targeting rules.
  /// </summary>
  public class TargetResolver {
    private readonly List<ICombatant> _playerTeam;
    private readonly List<ICombatant> _enemyTeam;
    private readonly List<ICombatant> _allCombatants;

    public TargetResolver(
      List<ICombatant> players,
      List<ICombatant> enemies,
      List<ICombatant> all) {
      _playerTeam = players;
      _enemyTeam = enemies;
      _allCombatants = all;
    }

    public List<ICombatant> GetValidTargets(ICombatant actor, TargetType targetType) {
      return targetType switch {
        TargetType.Single => GetEnemyTargets(actor),
        TargetType.Self => new List<ICombatant> { actor },
        TargetType.AllAllies => GetAllyTargets(actor),
        TargetType.AllEnemies => GetEnemyTargets(actor),
        TargetType.All => _allCombatants.Where(static c => c.IsAlive).ToList(),
        TargetType.Multiple => throw new System.NotImplementedException(),
        _ => new List<ICombatant>()
      };
    }

    public List<ICombatant> GetAllyTargets(ICombatant actor) {
      List<ICombatant> team = actor.Team == CombatTeam.Player ? _playerTeam : _enemyTeam;
      return team.Where(static c => c.IsAlive).ToList();
    }

    public List<ICombatant> GetEnemyTargets(ICombatant actor) {
      List<ICombatant> enemyTeam = actor.Team == CombatTeam.Player ? _enemyTeam : _playerTeam;
      return enemyTeam.Where(static c => c.IsAlive).ToList();
    }

    public ICombatant SelectTargetByStrategy(
      List<ICombatant> candidates,
      TargetingStrategy strategy) {
      return candidates == null || candidates.Count == 0
        ? null
        : strategy switch {
          TargetingStrategy.Random =>
            candidates[UnityEngine.Random.Range(0, candidates.Count)],

          TargetingStrategy.LowestHealth =>
            candidates.OrderBy(static c => c.GetStat(StatType.Health)).First(),

          TargetingStrategy.HighestHealth =>
            candidates.OrderByDescending(static c => c.GetStat(StatType.Health)).First(),

          TargetingStrategy.HighestThreat =>
            candidates.OrderByDescending(static c => c.GetStat(StatType.Attack)).First(),
          TargetingStrategy.Closest => throw new System.NotImplementedException(),
          _ => candidates[0]
        };
    }
  }
}
