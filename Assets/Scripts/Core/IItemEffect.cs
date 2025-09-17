using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Abstraction for an item effect used during combat.
  /// </summary>
  public interface IItemEffect
  {
    void Apply(ICombatant user, ICombatant target);
  }
}

