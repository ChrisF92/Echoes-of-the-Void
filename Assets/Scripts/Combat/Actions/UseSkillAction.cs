using System;
using EchoesOfTheVoid.Core;
using UnityEngine;

namespace EchoesOfTheVoid.Combat.Actions
{
  /// <summary>
  /// Executes a provided skill effect, consuming mana if supported.
  /// </summary>
  public sealed class UseSkillAction : ICombatAction
  {
    public string Name => _name;
    public int ManaCost => _effect.ManaCost;

    private readonly string _name;
    private readonly ISkillEffect _effect;

    public UseSkillAction(string name, ISkillEffect effect)
    {
      _name = string.IsNullOrWhiteSpace(name) ? "Use Skill" : name;
      _effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public void Execute(ICombatant user, ICombatant target)
    {
      if (user is IManaUser manaUser)
      {
        if (!manaUser.TryConsumeMana(_effect.ManaCost))
        {
          Debug.LogWarning($"[Action] {Name}: Not enough mana ({_effect.ManaCost}).");
          return;
        }
      }

      try
      {
        _effect.Execute(user, target);
        Debug.Log($"[Action] Skill '{Name}' used by {user?.Name ?? "?"} on {target?.Name ?? "?"}");
      }
      catch (Exception e)
      {
        Debug.LogException(e);
      }
    }
  }
}
