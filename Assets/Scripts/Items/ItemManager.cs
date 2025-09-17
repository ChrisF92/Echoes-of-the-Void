using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core;
using EchoesOfTheVoid.Combat;
using EchoesOfTheVoid.Combat.Actions;
using UnityEngine;

namespace EchoesOfTheVoid.Items
{
  /// <summary>
  /// Provides usable item actions for the current combat context.
  /// This minimal implementation uses a serialized list of item names
  /// and wraps each into a <see cref="UseItemAction"/> with a no-op effect.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class ItemManager : MonoBehaviour
  {
    [SerializeField] private List<string> _availableItemNames = new List<string>
    {
      "Potion",
      "Ether",
      "Antidote",
    };

    /// <summary>
    /// Returns item actions that are currently usable by <paramref name="user"/>.
    /// Filters out items that would have no valid targets.
    /// </summary>
    public List<ICombatAction> GetUsableItems(ICombatant user, TargetingSystem targeting)
    {
      var results = new List<ICombatAction>();
      if (user == null)
      {
        return results;
      }

      foreach (string name in _availableItemNames)
      {
        var action = new UseItemAction(name, new NoopItemEffect());
        if (targeting != null)
        {
          var candidates = targeting.GetValidTargets(action, user);
          if (candidates == null || candidates.Count == 0)
          {
            continue;
          }
        }
        results.Add(action);
      }
      return results;
    }

    private sealed class NoopItemEffect : IItemEffect
    {
      public void Apply(ICombatant user, ICombatant target) { }
    }
  }
}

