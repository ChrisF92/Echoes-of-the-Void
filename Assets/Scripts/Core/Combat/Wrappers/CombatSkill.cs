using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Wrappers {
  public class CombatSkill {
    public SkillScriptableObject Data { get; }

    public CombatSkill(SkillScriptableObject data) {
      Data = data;
    }

    public bool CanUse(ICombatant user) {
      return user.IsAlive && user.GetStat(StatType.Mana) >= Data.ManaCost;
    }

    public SkillResult Execute(ICombatant user, ICombatant target) {
      var effects = new List<CombatEffect>();

      foreach (SkillEffectData effectData in Data.Effects) {
        ICombatant actualTarget = effectData.TargetSelf ? user : target;
        int effectValue = CalculateEffectValue(effectData, user);

        effects.Add(new CombatEffect {
          Type = effectData.EffectType,
          Value = effectValue,
          Target = actualTarget
        });
      }

      foreach (CombatEffect effect in effects) {
        ApplyEffect(effect);
      }

      return SkillResult.Success($"{user.Name} uses {Data.DisplayName}!");
    }

    private int CalculateEffectValue(SkillEffectData effectData, ICombatant user) {
      int baseValue = effectData.BaseValue;

      if (effectData.StatScaling > 0f) {
        int statValue = user.GetStat(effectData.ScalingStat);
        baseValue += Mathf.RoundToInt(statValue * effectData.StatScaling);
      }

      return Mathf.RoundToInt(baseValue * effectData.DamageCurve.Evaluate(1f));
    }

    private void ApplyEffect(CombatEffect effect) {
      switch (effect.Type) {
        case EffectType.Damage:
          effect.Target.TakeDamage(effect.Value);
          break;
        case EffectType.Heal:
          effect.Target.Heal(effect.Value);
          break;
        case EffectType.Buff:
          break;
        case EffectType.Debuff:
          break;
        case EffectType.StatusEffect:
          break;
        default:
          break;
      }
    }
  }
}
