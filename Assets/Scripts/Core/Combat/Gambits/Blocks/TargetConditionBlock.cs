using System;
using EchoesOfTheVoid.Core.Combat.Entities;
using Sirenix.OdinInspector;

namespace EchoesOfTheVoid.Core.Combat.Gambits
{
  [Serializable]
  public abstract class TargetConditionBlock
  {
    [ShowInInspector, ReadOnly]
    public virtual string Summary => GetType().Name;

    public abstract bool TrySelectTarget(GambitRuntimeContext context, out ICombatant target, out string failureReason);
  }
}
