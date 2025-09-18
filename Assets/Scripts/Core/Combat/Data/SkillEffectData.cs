using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat
{
  [System.Serializable]
  public class SkillEffectData
  {
    public EffectType effectType;
    public int baseValue;
    public float statScaling = 0f;
    public StatType scalingStat;
    public bool targetSelf = false;
    public AnimationCurve damageCurve = AnimationCurve.Linear(0, 1, 1, 1);
  }
}

