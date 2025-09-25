using System;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Actions
{
  [Serializable]
  public struct CombatActionTiming
  {
    [Min(0f)] public float windup;
    [Min(0f)] public float resolution;
    [Min(0f)] public float recovery;

    public float Total => windup + resolution + recovery;

    public static CombatActionTiming Default => new CombatActionTiming
    {
      windup = 0.25f,
      resolution = 0.35f,
      recovery = 0.5f
    };
  }

  [Serializable]
  public struct CombatActionTimingOverride
  {
    public CombatActionType actionType;
    public CombatActionTiming timing;
  }
}
