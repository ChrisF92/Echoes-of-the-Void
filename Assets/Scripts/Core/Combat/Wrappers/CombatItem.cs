using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Data;

namespace EchoesOfTheVoid.Core.Combat.Wrappers {
  public class CombatItem {
    public ItemScriptableObject Data { get; }

    public CombatItem(ItemScriptableObject data) {
      Data = data;
    }

    public ItemResult Use(ICombatant user, ICombatant target) {
      StatusEffectManager statusManager = CombatSystem.Instance != null ? CombatSystem.Instance.StatusEffectManager : null;
      var effects = new List<CombatEffect>();

      foreach (ItemEffectData effectData in Data.Effects) {
        ICombatant actualTarget = effectData.TargetSelf ? user : (target ?? user);
        if (actualTarget == null) {
          continue;
        }

        var effect = new CombatEffect {
          Type = effectData.EffectType,
          Target = actualTarget,
          Value = effectData.Value,
          StatusEffect = effectData.StatusEffect
        };

        ApplyItemEffect(effect, statusManager);
        effects.Add(effect);
      }

      string message = CombatLogMessageBuilder.BuildActionMessage(
        user?.Name,
        Data.DisplayName,
        CombatLogMessageBuilder.BuildEffectSummaries(effects)
      );

      return ItemResult.Success(message, effects);
    }

    private void ApplyItemEffect(CombatEffect effect, StatusEffectManager statusManager) {
      if (effect.Target == null) {
        effect.AppliedValue = 0;
        return;
      }

      switch (effect.Type) {
        case EffectType.Heal: {
            int before = effect.Target.GetStat(StatType.Health);
            effect.Target.Heal(effect.Value);
            int after = effect.Target.GetStat(StatType.Health);
            effect.AppliedValue = Math.Max(0, after - before);
            break;
          }
        case EffectType.Damage: {
            int before = effect.Target.GetStat(StatType.Health);
            effect.Target.TakeDamage(effect.Value);
            int after = effect.Target.GetStat(StatType.Health);
            effect.AppliedValue = Math.Max(0, before - after);
            break;
          }
        case EffectType.ApplyStatus:
          effect.AppliedValue = 0;
          if (statusManager != null && effect.StatusEffect != null) {
            StatusEffect status = effect.StatusEffect.CreateInstance();
            statusManager.ApplyEffect(effect.Target, status);
          }
          break;
        default:
          effect.AppliedValue = 0;
          break;
      }
    }
  }
}


