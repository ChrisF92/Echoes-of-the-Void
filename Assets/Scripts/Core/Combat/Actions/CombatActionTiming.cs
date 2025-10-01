using System;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Actions {
  [Serializable]
  public struct CombatActionTiming {
    [Min(0f)] public float Windup;
    [Min(0f)] public float Resolution;
    [Min(0f)] public float Recovery;

    public readonly float Total => Windup + Resolution + Recovery;

    public static CombatActionTiming Default => new() {
      Windup = 0.25f,
      Resolution = 0.35f,
      Recovery = 0.5f
    };
  }

  [Serializable]
  public struct CombatActionTimingOverride {
    public CombatActionType ActionType;
    public CombatActionTiming Timing;
  }
}
