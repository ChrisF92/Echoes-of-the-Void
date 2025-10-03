using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat {
  [System.Serializable]
  public class SkillEffectData {
    public EffectType EffectType;

    [ShowIf(nameof(RequiresStatusEffect))]
    public StatusEffectSO StatusEffect;

    [HideIf(nameof(RequiresStatusEffect))]
    public int BaseValue;

    [HideIf(nameof(RequiresStatusEffect))]
    public float StatScaling = 0f;

    [HideIf(nameof(RequiresStatusEffect))]
    public StatType ScalingStat;

    public bool TargetSelf = false;

    [HideIf(nameof(RequiresStatusEffect))]
    public AnimationCurve DamageCurve = AnimationCurve.Linear(0, 1, 1, 1);

    private bool RequiresStatusEffect => EffectType == global::EchoesOfTheVoid.Core.Combat.EffectType.ApplyStatus;
  }
}

