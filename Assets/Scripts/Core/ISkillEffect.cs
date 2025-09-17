using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Abstraction for a skill effect used during combat.
  /// </summary>
  public interface ISkillEffect
  {
    int ManaCost { get; }
    void Execute(ICombatant user, ICombatant target);
  }
}

