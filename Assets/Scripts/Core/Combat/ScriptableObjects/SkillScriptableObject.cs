using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects {
  [CreateAssetMenu(fileName = "New Skill", menuName = "Combat/Skill")]
  public class SkillScriptableObject : ScriptableObject {
    [Header("Basic Info")]
    public string SkillId;
    public string DisplayName;
    [TextArea(2, 4)] public string Description;
    public Sprite Icon;

    [Header("Costs")]
    public int ManaCost;
    public int StaminaCost;
    public int CooldownTurns;

    [Header("Targeting")]
    public TargetType TargetType;
    public bool CanTargetSelf = true;
    public bool CanTargetAllies = false;
    public bool CanTargetEnemies = true;

    [Header("Effects")]
    public List<SkillEffectData> Effects = new();

    [Header("Animation & Audio")]
    public string AnimationTrigger;
    public AudioClip SoundEffect;
    public GameObject VisualEffect;
  }
}

