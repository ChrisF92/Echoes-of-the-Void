using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

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
        TargetType.Self => actor != null ? new List<ICombatant> { actor } : new List<ICombatant>(),
        TargetType.AllAllies => GetAllyTargets(actor),
        TargetType.AllEnemies => GetEnemyTargets(actor),
        TargetType.All => _allCombatants.Where(static c => c.IsAlive).ToList(),
        TargetType.Multiple => GetEnemyTargets(actor),
        _ => new List<ICombatant>()
      };
    }

    public List<ICombatant> ResolveSkillTargets(
      ICombatant actor,
      SkillSO skill,
      IReadOnlyList<ICombatant> requestedTargets) {
      if (actor == null || skill == null) {
        return new List<ICombatant>();
      }

      return skill.TargetType switch {
        TargetType.Self => new List<ICombatant> { actor },
        TargetType.AllAllies => GetAllyTargets(actor),
        TargetType.AllEnemies => GetEnemyTargets(actor),
        TargetType.All => _allCombatants.Where(static c => c.IsAlive).ToList(),
        TargetType.Multiple => FilterRequestedTargets(actor, skill, requestedTargets, true),
        TargetType.Single => FilterRequestedTargets(actor, skill, requestedTargets, false),
        _ => FilterRequestedTargets(actor, skill, requestedTargets, true)
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

    private List<ICombatant> FilterRequestedTargets(
      ICombatant actor,
      SkillSO skill,
      IReadOnlyList<ICombatant> requestedTargets,
      bool allowMultiple) {
      List<ICombatant> candidates = BuildCandidateTargets(actor, skill);
      if (candidates.Count == 0) {
        return new List<ICombatant>();
      }

      if (requestedTargets == null || requestedTargets.Count == 0) {
        return allowMultiple ? new List<ICombatant>(candidates) : new List<ICombatant>();
      }

      var result = new List<ICombatant>();
      var candidateSet = new HashSet<ICombatant>(candidates);

      foreach (ICombatant requested in requestedTargets) {
        if (requested == null || !candidateSet.Contains(requested) || result.Contains(requested)) {
          continue;
        }

        result.Add(requested);
        if (!allowMultiple) {
          break;
        }
      }

      return result;
    }

    private List<ICombatant> BuildCandidateTargets(ICombatant actor, SkillSO skill) {
      var candidates = new List<ICombatant>();
      if (actor == null || skill == null) {
        return candidates;
      }

      if (skill.CanTargetSelf && actor.IsAlive) {
        AddUniqueTarget(candidates, actor);
      }

      if (skill.CanTargetAllies) {
        foreach (ICombatant ally in GetAllyTargets(actor)) {
          if (ally == actor && !skill.CanTargetSelf) {
            continue;
          }

          AddUniqueTarget(candidates, ally);
        }
      }

      if (skill.CanTargetEnemies) {
        foreach (ICombatant enemy in GetEnemyTargets(actor)) {
          AddUniqueTarget(candidates, enemy);
        }
      }

      return candidates;
    }

    private static void AddUniqueTarget(List<ICombatant> list, ICombatant target) {
      if (target != null && !list.Contains(target)) {
        list.Add(target);
      }
    }
  }
}
