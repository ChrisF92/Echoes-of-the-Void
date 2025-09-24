using System;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations
{
  [Serializable]
  public class SelfTargetBlock : TargetConditionBlock
  {
    public override string Summary => "Self";

    public override bool TrySelectTarget(GambitRuntimeContext context, out ICombatant target, out string failureReason)
    {
      target = context?.Actor;
      if (target == null || !target.IsAlive)
      {
        failureReason = "Actor unavailable";
        return false;
      }

      failureReason = null;
      return true;
    }
  }
}
