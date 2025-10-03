using System;
using EchoesOfTheVoid.Core.Combat.Entities;
using Sirenix.OdinInspector;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations {
  [Serializable]
  public class AllyHealthBelowPercentBlock : TargetConditionBlock {
    [PropertyRange(0.05f, 1f)]
    public float Threshold = 0.5f;

    public bool IncludeSelf = true;

    public override string Summary => $"Ally HP < {(int)(Threshold * 100)}%";

    public override bool TrySelectTarget(GambitRuntimeContext context, out ICombatant target, out string failureReason) {
      target = null;
      if (context == null) {
        failureReason = "No context";
        return false;
      }

      foreach (ICombatant ally in context.Allies) {
        if (ally == null || !ally.IsAlive) {
          continue;
        }

        if (!IncludeSelf && ReferenceEquals(ally, context.Actor)) {
          continue;
        }

        int maxHealth = ally.GetMaxStat(StatType.Health);
        if (maxHealth <= 0) {
          continue;
        }

        int currentHealth = ally.GetStat(StatType.Health);
        float percent = (float)currentHealth / maxHealth;
        if (percent < Threshold) {
          target = ally;
          failureReason = null;
          return true;
        }
      }

      failureReason = "No ally under threshold";
      return false;
    }
  }
}
