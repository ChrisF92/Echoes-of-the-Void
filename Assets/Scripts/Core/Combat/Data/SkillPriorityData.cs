using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat
{
  [System.Serializable]
  public class SkillPriorityData
  {
    public SkillScriptableObject skill;
    [Range(0f, 1f)] public float priority;
  }
}

