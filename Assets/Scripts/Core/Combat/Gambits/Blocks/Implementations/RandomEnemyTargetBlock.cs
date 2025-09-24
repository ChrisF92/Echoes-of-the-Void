using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations
{
  [Serializable]
  public class RandomEnemyTargetBlock : TargetConditionBlock
  {
    public override string Summary => "Random Enemy";

    public override bool TrySelectTarget(GambitRuntimeContext context, out ICombatant target, out string failureReason)
    {
      target = null;
      if (context?.Enemies == null)
      {
        failureReason = "No enemies list";
        return false;
      }

      var candidates = new List<ICombatant>();
      foreach (var enemy in context.Enemies)
      {
        if (enemy != null && enemy.IsAlive)
        {
          candidates.Add(enemy);
        }
      }

      if (candidates.Count == 0)
      {
        failureReason = "No living enemies";
        return false;
      }

      var randomIndex = context.Random.Next(candidates.Count);
      target = candidates[randomIndex];
      failureReason = null;
      return true;
    }
  }
}
