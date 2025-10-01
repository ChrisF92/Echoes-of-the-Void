using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;

namespace EchoesOfTheVoid.Core.Combat.Components {
  public class InventoryComponent : CombatComponent {
    private const int DefaultCapacity = 30;
    private ICombatant _owner;

    public InventoryComponent() : this(DefaultCapacity) {
    }

    public InventoryComponent(int capacity) {
      Inventory = new ItemInventory(capacity > 0 ? capacity : DefaultCapacity);
    }

    public ItemInventory Inventory { get; }

    public event System.Action<int, InventorySlot> OnSlotChanged {
      add => Inventory.OnSlotChanged += value;
      remove => Inventory.OnSlotChanged -= value;
    }

    public event System.Action<ItemScriptableObject, int> OnItemAdded {
      add => Inventory.OnItemAdded += value;
      remove => Inventory.OnItemAdded -= value;
    }

    public event System.Action<ItemScriptableObject, int> OnItemRemoved {
      add => Inventory.OnItemRemoved += value;
      remove => Inventory.OnItemRemoved -= value;
    }

    public override void Initialize(ICombatant owner) {
      _owner = owner;
    }

    public override void Update(float deltaTime) {
    }

    public bool AddItem(ItemScriptableObject itemData, int quantity = 1) {
      if (itemData == null || quantity <= 0) {
        return false;
      }

      bool success = Inventory.AddItem(itemData, quantity, out int added);
      if (!success) {
        Debug.LogWarning($"InventoryComponent on {_owner?.Name ?? "Unknown"} could not store all of {itemData.DisplayName} ({added}/{quantity}).");
      }

      return success;
    }

    public bool RemoveItem(ItemScriptableObject itemData, int quantity = 1) {
      return itemData != null && Inventory.RemoveItem(itemData, quantity, out _);
    }

    public bool RemoveItem(string itemId, int quantity = 1) {
      return Inventory.RemoveItem(itemId, quantity, out _);
    }

    public bool HasItem(string itemId, int quantity = 1) {
      return Inventory.HasItem(itemId, quantity);
    }

    public bool HasItem(ItemScriptableObject itemData, int quantity = 1) {
      return Inventory.HasItem(itemData, quantity);
    }

    public int GetItemCount(string itemId) {
      return Inventory.GetItemCount(itemId);
    }

    public int GetItemCount(ItemScriptableObject itemData) {
      return Inventory.GetItemCount(itemData);
    }

    public ItemResult UseItem(ItemScriptableObject itemData, ICombatant target = null) {
      if (itemData == null) {
        return ItemResult.Failed("Item not set");
      }

      if (!HasItem(itemData)) {
        return ItemResult.Failed("Item not available");
      }

      if (!itemData.ConsumableInCombat) {
        return ItemResult.Failed("Cannot use this item in combat");
      }

      var item = new CombatItem(itemData);
      ItemResult result = item.Use(_owner, target);

      if (result.IsSuccess) {
        _ = Inventory.RemoveItem(itemData, 1, out _);
      }

      return result;
    }

    public IEnumerable<ItemScriptableObject> GetUsableItems() {
      var seen = new HashSet<string>();
      foreach (InventorySlot slot in Inventory.Slots) {
        ItemScriptableObject item = slot.Item;
        if (item == null || !item.ConsumableInCombat) {
          continue;
        }

        if (seen.Add(item.ItemId)) {
          yield return item;
        }
      }
    }
  }
}
