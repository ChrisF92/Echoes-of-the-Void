using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Inventory.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Run {
  /// <summary>
  /// Design-time container for rewards granted by a combat run floor or upon completion.
  /// </summary>
  [Serializable]
  public class CombatRunRewardBundle {
    [SerializeField, Min(0)] private int _experience;
    [SerializeField, Min(0)] private int _currency;
    [SerializeField] private List<ItemStackData> _items = new();

    public int Experience => Mathf.Max(0, _experience);
    public int Currency => Mathf.Max(0, _currency);
    public IReadOnlyList<ItemStackData> Items => _items != null ? _items : Array.Empty<ItemStackData>();

    public bool IsEmpty {
      get {
        if (Experience > 0 || Currency > 0) {
          return false;
        }

        if (_items == null) {
          return true;
        }

        for (int i = 0; i < _items.Count; i++) {
          ItemStackData stack = _items[i];
          if (stack != null && stack.Item != null && stack.Quantity > 0) {
            return false;
          }
        }

        return true;
      }
    }
  }
}
