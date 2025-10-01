using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat {
  [System.Serializable]
  public class SkillEffectData {
    public EffectType EffectType;
    public int BaseValue;
    public float StatScaling = 0f;
    public StatType ScalingStat;
    public bool TargetSelf = false;
    public AnimationCurve DamageCurve = AnimationCurve.Linear(0, 1, 1, 1);
  }
}

