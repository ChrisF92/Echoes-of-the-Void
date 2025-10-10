using System.Collections.Generic;
using System.Linq;

using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Inventory.Data;
using ItemData = EchoesOfTheVoid.Core.Inventory.ScriptableObjects.ItemScriptableObject;

namespace EchoesOfTheVoid.UI.Combat {
  /// <summary>
  /// Provides helper methods for resolving valid targets for combat actions.
  /// </summary>
  public static class CombatTargetingService {
    public static IReadOnlyList<Combatant> GetValidTargets(
      CombatActionType actionType,
      Combatant caster,
      ItemData pendingItem,
      SkillSO pendingSkill,
      IReadOnlyList<Combatant> playerTeam,
      IReadOnlyList<Combatant> enemyTeam) {

      var results = new List<Combatant>();
      if (caster == null) {
        return results;
      }

      IReadOnlyList<Combatant> player = playerTeam ?? System.Array.Empty<Combatant>();
      IReadOnlyList<Combatant> enemy = enemyTeam ?? System.Array.Empty<Combatant>();

      switch (actionType) {
        case CombatActionType.Attack:
          results.AddRange(caster.IsPlayerControlled ? enemy : player);
          break;
        case CombatActionType.Defend:
          results.Add(caster);
          break;
        case CombatActionType.Item:
          results.AddRange(GetTargetsForItem(pendingItem, caster, player, enemy));
          break;
        case CombatActionType.Skill:
          results.AddRange(GetTargetsForSkill(pendingSkill, caster, player, enemy));
          break;
      }

      return results
        .Where(static combatant => combatant != null && combatant.IsAlive)
        .Distinct()
        .ToList();
    }

    public static bool ItemRequiresTarget(ItemData item) {
      return item != null && item.Effects.Any(effect => !effect.TargetSelf);
    }

    public static bool SkillRequiresTarget(SkillSO skill) {
      if (skill == null) {
        return false;
      }

      return skill.TargetType switch {
        TargetType.Self => false,
        TargetType.All => false,
        TargetType.AllAllies => false,
        TargetType.Multiple => true,
        TargetType.AllEnemies => true,
        TargetType.Single => skill.CanTargetAllies || skill.CanTargetEnemies,
        _ => skill.CanTargetEnemies || skill.CanTargetAllies
      };
    }

    private static IEnumerable<Combatant> GetTargetsForItem(ItemData item, Combatant caster, IReadOnlyList<Combatant> playerTeam, IReadOnlyList<Combatant> enemyTeam) {
      if (item == null || caster == null) {
        return new List<Combatant> { caster };
      }

      if (!ItemRequiresTarget(item)) {
        return new List<Combatant> { caster };
      }

      List<ItemEffectData> effects = item.Effects.Where(effect => !effect.TargetSelf).ToList();
      if (effects.Count == 0) {
        return new List<Combatant> { caster };
      }

      bool hasDamage = effects.Any(effect => effect.EffectType == EffectType.Damage);
      bool hasHeal = effects.Any(effect => effect.EffectType == EffectType.Heal);
      bool hasDebuff = effects.Any(effect =>
        effect.EffectType == EffectType.ApplyStatus &&
        effect.StatusEffect != null &&
        effect.StatusEffect.IsDebuff);
      bool hasBuff = effects.Any(effect =>
        effect.EffectType == EffectType.ApplyStatus &&
        effect.StatusEffect != null &&
        !effect.StatusEffect.IsDebuff);

      bool targetsEnemies = hasDamage || hasDebuff;
      bool targetsAllies = hasHeal || hasBuff;

      if (targetsEnemies && targetsAllies) {
        return playerTeam.Concat(enemyTeam).Where(static combatant => combatant != null && combatant.IsAlive).ToList();
      }

      if (targetsEnemies) {
        return caster.IsPlayerControlled ? enemyTeam : playerTeam;
      }

      if (targetsAllies) {
        return caster.IsPlayerControlled ? playerTeam : enemyTeam;
      }

      return new List<Combatant> { caster };
    }

    private static IEnumerable<Combatant> GetTargetsForSkill(SkillSO skill, Combatant caster, IReadOnlyList<Combatant> playerTeam, IReadOnlyList<Combatant> enemyTeam) {
      if (skill == null || caster == null) {
        return new List<Combatant> { caster };
      }

      IReadOnlyList<Combatant> allies = caster.IsPlayerControlled ? playerTeam : enemyTeam;
      IReadOnlyList<Combatant> enemies = caster.IsPlayerControlled ? enemyTeam : playerTeam;

      return skill.TargetType switch {
        TargetType.Self => new List<Combatant> { caster },
        TargetType.AllAllies => allies,
        TargetType.AllEnemies => enemies,
        TargetType.All => playerTeam.Concat(enemyTeam).Where(static combatant => combatant != null && combatant.IsAlive).ToList(),
        TargetType.Multiple => enemies,
        TargetType.Single => GetSingleTargetCandidates(skill, caster, allies, enemies),
        _ => BuildSkillTargetsByAffinity(skill, caster, allies, enemies)
      };
    }

    private static IEnumerable<Combatant> GetSingleTargetCandidates(SkillSO skill, Combatant caster, IReadOnlyList<Combatant> allies, IReadOnlyList<Combatant> enemies) {
      var results = new List<Combatant>();

      if (skill.CanTargetSelf && caster.IsAlive) {
        results.Add(caster);
      }

      if (skill.CanTargetAllies) {
        results.AddRange(allies.Where(combatant => combatant != null && combatant.IsAlive && combatant != caster));
      }

      if (skill.CanTargetEnemies) {
        results.AddRange(enemies.Where(static combatant => combatant != null && combatant.IsAlive));
      }

      return results;
    }

    private static IEnumerable<Combatant> BuildSkillTargetsByAffinity(SkillSO skill, Combatant caster, IReadOnlyList<Combatant> allies, IReadOnlyList<Combatant> enemies) {
      var results = new List<Combatant>();

      if (skill.CanTargetSelf && caster.IsAlive) {
        results.Add(caster);
      }

      if (skill.CanTargetAllies) {
        results.AddRange(allies.Where(static combatant => combatant != null && combatant.IsAlive));
      }

      if (skill.CanTargetEnemies) {
        results.AddRange(enemies.Where(static combatant => combatant != null && combatant.IsAlive));
      }

      return results;
    }
  }
}
