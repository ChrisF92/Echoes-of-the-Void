using System;
using EchoesOfTheVoid.Core;
using UnityEngine;

namespace EchoesOfTheVoid.Combat.Actions
{
  /// <summary>
  /// Uses a provided item effect.
  /// </summary>
  public sealed class UseItemAction : ICombatAction
  {
    public string Name => _name;

    private readonly string _name;
    private readonly IItemEffect _effect;

    public UseItemAction(string name, IItemEffect effect)
    {
      _name = string.IsNullOrWhiteSpace(name) ? "Use Item" : name;
      _effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public void Execute(ICombatant user, ICombatant target)
    {
      try
      {
        _effect.Apply(user, target);
        Debug.Log($"[Action] Item '{Name}' used by {user?.Name ?? "?"} on {target?.Name ?? "?"}");
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
    }
  }
}
