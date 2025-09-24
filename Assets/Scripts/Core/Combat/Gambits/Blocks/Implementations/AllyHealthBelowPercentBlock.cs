using System;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;
using Sirenix.OdinInspector;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations
{
  [Serializable]
  public class AllyHealthBelowPercentBlock : TargetConditionBlock
  {
    [PropertyRange(0.05f, 1f)]
    public float threshold = 0.5f;

    public bool includeSelf = true;

    public override string Summary => $"Ally HP < {(int)(threshold * 100)}%";

    public override bool TrySelectTarget(GambitRuntimeContext context, out ICombatant target, out string failureReason)
    {
      target = null;
      if (context == null)
      {
        failureReason = "No context";
        return false;
      }

      foreach (var ally in context.Allies)
      {
        if (ally == null || !ally.IsAlive)
        {
          continue;
        }

        if (!includeSelf && ally == context.Actor)
        {
          continue;
        }

        var maxHealth = ally.GetMaxStat(StatType.Health);
        if (maxHealth <= 0)
        {
          continue;
        }

        var currentHealth = ally.GetStat(StatType.Health);
        var percent = (float)currentHealth / maxHealth;
        if (percent < threshold)
        {
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
