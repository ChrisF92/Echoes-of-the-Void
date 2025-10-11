using System.Collections.Generic;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Combat.Run {
  /// <summary>
  /// Aggregates rewards earned across a combat run.
  /// </summary>
  public sealed class CombatRunRewards {
    private readonly Dictionary<ItemScriptableObject, int> _itemTotals = new();

    public int Experience { get; private set; }
    public int EchoExperience { get; private set; }
    public int Currency { get; private set; }

    public IReadOnlyDictionary<ItemScriptableObject, int> ItemTotals => _itemTotals;

    public bool IsEmpty => Experience <= 0 && EchoExperience <= 0 && Currency <= 0 && _itemTotals.Count == 0;

    public void Clear() {
      Experience = 0;
      EchoExperience = 0;
      Currency = 0;
      _itemTotals.Clear();
    }

    public void Add(CombatRunRewardBundle bundle) {
      if (bundle == null || bundle.IsEmpty) {
        return;
      }

      Experience += bundle.Experience;
      EchoExperience += bundle.EchoExperience;
      Currency += bundle.Currency;

      foreach (ItemStackData stack in bundle.Items) {
        if (stack == null || stack.Item == null || stack.Quantity <= 0) {
          continue;
        }

        if (_itemTotals.TryGetValue(stack.Item, out int current)) {
          _itemTotals[stack.Item] = current + stack.Quantity;
        } else {
          _itemTotals[stack.Item] = stack.Quantity;
        }
      }
    }

    public void Add(CombatRunRewards other) {
      if (other == null || other.IsEmpty) {
        return;
      }

      Experience += other.Experience;
      EchoExperience += other.EchoExperience;
      Currency += other.Currency;

      foreach (KeyValuePair<ItemScriptableObject, int> entry in other._itemTotals) {
        if (_itemTotals.TryGetValue(entry.Key, out int current)) {
          _itemTotals[entry.Key] = current + entry.Value;
        } else {
          _itemTotals[entry.Key] = entry.Value;
        }
      }
    }

    public List<ItemStackData> ToItemStacks() {
      var result = new List<ItemStackData>(_itemTotals.Count);
      foreach (KeyValuePair<ItemScriptableObject, int> entry in _itemTotals) {
        if (entry.Key == null || entry.Value <= 0) {
          continue;
        }

        result.Add(new ItemStackData {
          Item = entry.Key,
          Quantity = entry.Value
        });
      }

      return result;
    }
  }
}
