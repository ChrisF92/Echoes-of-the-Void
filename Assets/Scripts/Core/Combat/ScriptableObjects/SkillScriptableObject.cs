using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat;

namespace EchoesOfTheVoid.Core.Combat.ScriptableObjects
{
  [CreateAssetMenu(fileName = "New Skill", menuName = "Combat/Skill")]
  public class SkillScriptableObject : ScriptableObject
  {
    [Header("Basic Info")]
    public string skillId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Costs")]
    public int manaCost;
    public int staminaCost;
    public float cooldownTime;

    [Header("Targeting")]
    public TargetType targetType;
    public bool canTargetSelf = true;
    public bool canTargetAllies = false;
    public bool canTargetEnemies = true;

    [Header("Effects")]
    public List<SkillEffectData> effects = new();

    [Header("Animation & Audio")]
    public string animationTrigger;
    public AudioClip soundEffect;
    public GameObject visualEffect;
  }
}

