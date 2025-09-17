using System;
using EchoesOfTheVoid.Core;
using UnityEngine;

namespace EchoesOfTheVoid.Combat.Actions
{
  /// <summary>
  /// Enters a defensive stance, reducing incoming damage for a number of turns.
  /// </summary>
  public sealed class DefendAction : ICombatAction
  {
    public string Name => "Defend";
    public int Turns => _turns;
    public float DamageReduction => _damageReduction;

    private readonly int _turns;
    private readonly float _damageReduction;

    /// <param name="turns">Number of turns the stance lasts (>= 1).</param>
    /// <param name="damageReduction">Fraction (0..1) to reduce incoming damage.</param>
    public DefendAction(int turns, float damageReduction)
    {
      _turns = Mathf.Max(1, turns);
      _damageReduction = Mathf.Clamp01(damageReduction);
    }

    public void Execute(ICombatant user, ICombatant target)
    {
      if (user is not IDefendable defendable)
      {
        Debug.LogWarning("[Action] Defend: User cannot defend.");
        return;
      }

      defendable.ApplyDefense(_turns, _damageReduction);
      Debug.Log($"[Action] Defend applied: turns={_turns} reduction={_damageReduction}");
    }
  }
}
