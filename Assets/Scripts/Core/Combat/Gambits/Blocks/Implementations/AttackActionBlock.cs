using System;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations
{
  [Serializable]
  public class AttackActionBlock : GambitActionBlock
  {
    public override string Summary => "Attack";

    public override bool TryBuildAction(GambitRuntimeContext context, ICombatant target, out CombatAction action, out string failureReason)
    {
      action = null;
      if (target == null || !target.IsAlive)
      {
        failureReason = "Invalid target";
        return false;
      }

      action = new CombatAction
      {
        ActionType = CombatActionType.Attack,
        Target = target
      };

      failureReason = null;
      return true;
    }
  }
}
