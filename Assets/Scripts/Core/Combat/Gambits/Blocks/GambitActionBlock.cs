using System;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Entities;
using Sirenix.OdinInspector;

namespace EchoesOfTheVoid.Core.Combat.Gambits {
  [Serializable]
  public abstract class GambitActionBlock {
    [ShowInInspector, ReadOnly]
    public virtual string Summary => GetType().Name;

    public abstract bool TryBuildAction(GambitRuntimeContext context, ICombatant target, out CombatAction action, out string failureReason);
  }
}
