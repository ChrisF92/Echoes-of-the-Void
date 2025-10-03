using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Wrappers {
  /// <summary>
  /// Enhanced combat skill wrapper with status effect support.
  /// </summary>
  public class CombatSkill {
    public SkillSO Data { get; }

    public CombatSkill(SkillSO data) {
      Data = data;
    }

    public bool CanUse(ICombatant user) {
      return user.IsAlive && user.GetStat(StatType.Mana) >= Data.ManaCost;
    }

    public SkillResult Execute(ICombatant user, ICombatant target) {
      var effects = new List<CombatEffect>();
      DamageCalculator damageCalculator = CombatSystem.Instance != null ? CombatSystem.Instance.DamageCalculator : null;
      StatusEffectManager statusEffectManager = CombatSystem.Instance != null ? CombatSystem.Instance.StatusEffectManager : null;

      foreach (SkillEffectData effectData in Data.Effects) {
        ICombatant actualTarget = effectData.TargetSelf ? user : target;
        if (actualTarget == null) {
          continue;
        }

        switch (effectData.EffectType) {
          case EffectType.Damage:
          case EffectType.Heal: {
              int effectValue;
              if (damageCalculator != null) {
                effectValue = damageCalculator.CalculateSkillDamage(
                  effectData.BaseValue,
                  effectData.StatScaling,
                  effectData.ScalingStat,
                  user,
                  effectData.DamageCurve
                );
              } else {
                effectValue = CalculateEffectValueFallback(effectData, user);
              }

              effects.Add(new CombatEffect {
                Type = effectData.EffectType,
                Value = effectValue,
                Target = actualTarget
              });
              break;
            }

          case EffectType.ApplyStatus:
            if (effectData.StatusEffect == null) {
              continue;
            }

            effects.Add(new CombatEffect {
              Type = EffectType.ApplyStatus,
              Target = actualTarget,
              StatusEffect = effectData.StatusEffect
            });
            break;

          default:
            break;
        }
      }

      foreach (CombatEffect effect in effects) {
        ApplyEffect(effect, statusEffectManager);
      }

      string message = CombatLogMessageBuilder.BuildActionMessage(
        user?.Name,
        Data.DisplayName,
        CombatLogMessageBuilder.BuildEffectSummaries(effects)
      );

      return SkillResult.Success(message, effects);
    }

    private int CalculateEffectValueFallback(SkillEffectData effectData, ICombatant user) {
      int baseValue = effectData.BaseValue;

      if (effectData.StatScaling > 0f) {
        int statValue = user.GetStat(effectData.ScalingStat);
        baseValue += Mathf.RoundToInt(statValue * effectData.StatScaling);
      }

      return Mathf.RoundToInt(baseValue * effectData.DamageCurve.Evaluate(1f));
    }

    private void ApplyEffect(CombatEffect effect, StatusEffectManager statusManager) {
      if (effect.Target == null) {
        effect.AppliedValue = 0;
        return;
      }

      switch (effect.Type) {
        case EffectType.Damage: {
            int before = effect.Target.GetStat(StatType.Health);
            effect.Target.TakeDamage(effect.Value);
            int after = effect.Target.GetStat(StatType.Health);
            effect.AppliedValue = Math.Max(0, before - after);
            break;
          }

        case EffectType.Heal: {
            int before = effect.Target.GetStat(StatType.Health);
            effect.Target.Heal(effect.Value);
            int after = effect.Target.GetStat(StatType.Health);
            effect.AppliedValue = Math.Max(0, after - before);
            break;
          }

        case EffectType.ApplyStatus:
          effect.AppliedValue = 0;
          if (statusManager != null && effect.StatusEffect != null) {
            StatusEffect statusEffect = effect.StatusEffect.CreateInstance();
            statusManager.ApplyEffect(effect.Target, statusEffect);
          }
          break;

        default:
          effect.AppliedValue = 0;
          break;
      }
    }
  }
}


