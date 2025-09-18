using System.Collections.Generic;
using System.Linq;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using EchoesOfTheVoid.Core.Inventory.Database;

namespace EchoesOfTheVoid.Core.Combat.Components
{
  public class InventoryComponent : CombatComponent
  {
    private readonly Dictionary<string, int> _items = new();
    private ICombatant _owner;

    public override void Initialize(ICombatant owner)
    {
      _owner = owner;
    }

    public override void Update(float deltaTime)
    {
    }

    public void AddItem(ItemScriptableObject itemData, int quantity = 1)
    {
      if (_items.ContainsKey(itemData.itemId))
      {
        _items[itemData.itemId] = System.Math.Min(_items[itemData.itemId] + quantity, itemData.maxStackSize);
      }
      else
      {
        _items[itemData.itemId] = System.Math.Min(quantity, itemData.maxStackSize);
      }
    }

    public bool HasItem(string itemId, int quantity = 1)
    {
      return _items.TryGetValue(itemId, out var count) && count >= quantity;
    }

    public ItemResult UseItem(ItemScriptableObject itemData, ICombatant target = null)
    {
      if (!HasItem(itemData.itemId))
      {
        return ItemResult.Failed("Item not available");
      }

      if (!itemData.consumableInCombat)
      {
        return ItemResult.Failed("Cannot use this item in combat");
      }

      var item = new CombatItem(itemData);
      var result = item.Use(_owner, target);

      if (result.IsSuccess)
      {
        _items[itemData.itemId]--;
        if (_items[itemData.itemId] <= 0)
        {
          _items.Remove(itemData.itemId);
        }
      }

      return result;
    }

    public IEnumerable<ItemScriptableObject> GetUsableItems()
    {
      return ItemDatabase.Instance.GetItems().Where(item => HasItem(item.itemId) && item.consumableInCombat);
    }
  }
}
