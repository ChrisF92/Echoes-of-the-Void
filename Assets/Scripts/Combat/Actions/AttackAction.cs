using System;
using EchoesOfTheVoid.Core;
using UnityEngine;

namespace EchoesOfTheVoid.Combat.Actions
{
  /// <summary>
  /// Simple damaging action. Optionally respects a target's defend mitigation.
  /// </summary>
  public sealed class AttackAction : ICombatAction
  {
    public string Name => "Attack";
    public int Damage => _damage;

    private readonly int _damage;

    public AttackAction(int damage)
    {
      _damage = Mathf.Max(0, damage);
    }

    public void Execute(ICombatant user, ICombatant target)
    {
      if (target is not IDamageable damageable)
      {
        Debug.LogWarning($"[Action] Attack: target {(target?.Name ?? "null")} is not damageable.");
        return;
      }

      int outgoing = _damage;
      if (target is IDefendable defendable)
      {
        outgoing = defendable.MitigateDamage(outgoing);
      }

      if (outgoing <= 0)
      {
        Debug.Log($"[Action] Attack mitigated to 0 on {target.Name}.");
        return;
      }

      damageable.ApplyDamage(outgoing);
      Debug.Log($"[Action] Attack applied {outgoing} damage to {target.Name}.");
    }
  }
}
