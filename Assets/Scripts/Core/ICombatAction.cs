using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Abstraction for an executable combat action.
  /// Kept minimal so concrete actions can define their own data/behavior.
  /// </summary>
  public interface ICombatAction
  {
    /// <summary>
    /// A display-friendly name for the action.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the action from <paramref name="user"/> onto <paramref name="target"/>.
    /// Implementations decide effect resolution; may throw for invalid use.
    /// </summary>
    void Execute(ICombatant user, ICombatant target);
  }
}

