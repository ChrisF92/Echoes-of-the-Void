using System;

namespace EchoesOfTheVoid.Core
{
  /// <summary>
  /// Contract for any unit that can participate in turn-based combat.
  /// Implementations should be lightweight and free of manager logic.
  /// </summary>
  public interface ICombatant
  {
    /// <summary>
    /// A display-friendly name for the combatant (for logs/UI).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Current health of the combatant. Zero or less implies not alive.
    /// </summary>
    int Health { get; }

    /// <summary>
    /// Maximum health of the combatant.
    /// </summary>
    int MaxHealth { get; }

    /// <summary>
    /// Current mana or resource pool used by actions.
    /// </summary>
    int Mana { get; }

    /// <summary>
    /// Whether this combatant is able to take a turn (e.g., not defeated).
    /// Turn selection skips combatants where this is <c>false</c>.
    /// </summary>
    bool IsAlive { get; }

    /// <summary>
    /// Called by the <see cref="TurnManager"/> when this combatant's turn begins.
    /// Implementations should prepare internal state for taking actions.
    /// </summary>
    void BeginTurn();

    /// <summary>
    /// Called by the <see cref="TurnManager"/> when this combatant's turn ends.
    /// Implementations should finalize state or dispatch any end-of-turn effects.
    /// </summary>
    void EndTurn();

    /// <summary>
    /// Performs a combat action targeting another combatant.
    /// Implementations should validate inputs and internal state.
    /// </summary>
    /// <param name="action">The action to perform.</param>
    /// <param name="target">The target of the action.</param>
    void PerformAction(ICombatAction action, ICombatant target);
  }
}
