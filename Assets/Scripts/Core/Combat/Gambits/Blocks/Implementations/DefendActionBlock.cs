using System;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations {
  [Serializable]
  public class DefendActionBlock : GambitActionBlock {
    public override string Summary => "Defend";

    public override bool TryBuildAction(GambitRuntimeContext context, ICombatant target, out CombatAction action, out string failureReason) {
      action = new CombatAction {
        ActionType = CombatActionType.Defend,
        Target = context?.Actor
      };

      failureReason = null;
      return true;
    }
  }
}
