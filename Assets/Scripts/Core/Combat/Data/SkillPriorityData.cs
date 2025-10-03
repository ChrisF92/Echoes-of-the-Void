using UnityEngine;

using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat {
  [System.Serializable]
  public class SkillPriorityData {
    public SkillSO Skill;
    [Range(0f, 1f)] public float Priority;
  }
}

