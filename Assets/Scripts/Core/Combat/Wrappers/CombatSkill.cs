using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Wrappers
{
  public class CombatSkill
  {
    public SkillScriptableObject Data { get; }

    public CombatSkill(SkillScriptableObject data)
    {
      Data = data;
    }

    public bool CanUse(ICombatant user)
    {
      return user.IsAlive && user.GetStat(StatType.Mana) >= Data.manaCost;
    }

    public SkillResult Execute(ICombatant user, ICombatant target)
    {
      var effects = new List<CombatEffect>();

      foreach (var effectData in Data.effects)
      {
        var actualTarget = effectData.targetSelf ? user : target;
        var effectValue = CalculateEffectValue(effectData, user);

        effects.Add(new CombatEffect
        {
          Type = effectData.effectType,
          Value = effectValue,
          Target = actualTarget
        });
      }

      foreach (var effect in effects)
      {
        ApplyEffect(effect);
      }

      return SkillResult.Success($"{user.Name} uses {Data.displayName}!");
    }

    private int CalculateEffectValue(SkillEffectData effectData, ICombatant user)
    {
      var baseValue = effectData.baseValue;

      if (effectData.statScaling > 0f)
      {
        var statValue = user.GetStat(effectData.scalingStat);
        baseValue += Mathf.RoundToInt(statValue * effectData.statScaling);
      }

      return Mathf.RoundToInt(baseValue * effectData.damageCurve.Evaluate(1f));
    }

    private void ApplyEffect(CombatEffect effect)
    {
      switch (effect.Type)
      {
        case EffectType.Damage:
          effect.Target.TakeDamage(effect.Value);
          break;
        case EffectType.Heal:
          effect.Target.Heal(effect.Value);
          break;
      }
    }
  }
}
