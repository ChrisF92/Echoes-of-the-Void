using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Database;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Inventory.Systems {
  public class ItemInventory {
    private readonly List<InventorySlot> _slots;

    public int Capacity => _slots.Count;
    public IReadOnlyList<InventorySlot> Slots => _slots;

    public bool IsEmpty {
      get {
        for (int i = 0; i < _slots.Count; i++) {
          if (!_slots[i].IsEmpty) {
            return false;
          }
        }

        return true;
      }
    }

    public bool IsFull {
      get {
        for (int i = 0; i < _slots.Count; i++) {
          InventorySlot slot = _slots[i];
          if (slot.IsEmpty || slot.RemainingCapacity > 0) {
            return false;
          }
        }

        return true;
      }
    }

    public event Action<int, InventorySlot> OnSlotChanged;
    public event Action<ItemScriptableObject, int> OnItemAdded;
    public event Action<ItemScriptableObject, int> OnItemRemoved;

    public ItemInventory(int capacity) {
      if (capacity <= 0) {
        throw new ArgumentOutOfRangeException(nameof(capacity), "Inventory capacity must be greater than zero.");
      }

      _slots = new List<InventorySlot>(capacity);
      for (int i = 0; i < capacity; i++) {
        _slots.Add(new InventorySlot());
      }
    }

    public ItemInventory(int capacity, IEnumerable<ItemStackData> startingItems) : this(capacity) {
      Load(startingItems, suppressNotifications: true);
    }

    public bool CanAdd(ItemScriptableObject item, int quantity = 1) {
      if (item == null || quantity <= 0) {
        return false;
      }

      int remaining = quantity;
      int maxStack = Mathf.Max(1, item.MaxStackSize);

      if (maxStack > 1) {
        for (int i = 0; i < _slots.Count && remaining > 0; i++) {
          InventorySlot slot = _slots[i];
          if (!slot.CanStack(item)) {
            continue;
          }

          remaining -= slot.AvailableSpaceFor(item);
        }
      }

      for (int i = 0; i < _slots.Count && remaining > 0; i++) {
        InventorySlot slot = _slots[i];
        if (!slot.IsEmpty) {
          continue;
        }

        remaining -= slot.AvailableSpaceFor(item);
      }

      return remaining <= 0;
    }

    public bool CanAdd(string itemId, int quantity = 1, ItemDatabase database = null) {
      if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) {
        return false;
      }

      database ??= ItemDatabase.Instance;
      if (database == null) {
        return false;
      }

      ItemScriptableObject item = database.GetItem(itemId);
      return item != null && CanAdd(item, quantity);
    }

    public bool AddItem(ItemScriptableObject item, int quantity = 1) {
      return AddItem(item, quantity, out _);
    }

    public bool AddItem(ItemScriptableObject item, int quantity, out int addedAmount) {
      addedAmount = 0;
      if (item == null || quantity <= 0) {
        return false;
      }

      int remaining = quantity;
      int maxStack = Mathf.Max(1, item.MaxStackSize);

      if (maxStack > 1) {
        for (int i = 0; i < _slots.Count && remaining > 0; i++) {
          InventorySlot slot = _slots[i];
          if (!slot.CanStack(item)) {
            continue;
          }

          int added = slot.Add(item, remaining);
          if (added <= 0) {
            continue;
          }

          remaining -= added;
          addedAmount += added;
          PublishAdded(i, slot, item, added);
        }
      }

      for (int i = 0; i < _slots.Count && remaining > 0; i++) {
        InventorySlot slot = _slots[i];
        if (!slot.IsEmpty) {
          continue;
        }

        int added = slot.Add(item, remaining);
        if (added <= 0) {
          continue;
        }

        remaining -= added;
        addedAmount += added;
        PublishAdded(i, slot, item, added);
      }

      return remaining == 0;
    }

    public bool AddItem(string itemId, int quantity = 1, ItemDatabase database = null) {
      return AddItem(itemId, quantity, out _, database);
    }

    public bool AddItem(string itemId, int quantity, out int addedAmount, ItemDatabase database = null) {
      addedAmount = 0;
      if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) {
        return false;
      }

      database ??= ItemDatabase.Instance;
      if (database == null) {
        Debug.LogWarning("ItemInventory.AddItem called without an ItemDatabase instance.");
        return false;
      }

      ItemScriptableObject item = database.GetItem(itemId);
      if (item == null) {
        Debug.LogWarning($"ItemInventory.AddItem could not find item with id '{itemId}'.");
        return false;
      }

      return AddItem(item, quantity, out addedAmount);
    }

    public bool RemoveItem(ItemScriptableObject item, int quantity = 1) {
      return RemoveItem(item, quantity, out _);
    }

    public bool RemoveItem(ItemScriptableObject item, int quantity, out int removedAmount) {
      removedAmount = 0;
      if (item == null || quantity <= 0) {
        return false;
      }

      int remaining = quantity;

      for (int i = 0; i < _slots.Count && remaining > 0; i++) {
        InventorySlot slot = _slots[i];
        if (slot.IsEmpty || slot.Item != item) {
          continue;
        }

        int removed = slot.Remove(remaining);
        if (removed <= 0) {
          continue;
        }

        remaining -= removed;
        removedAmount += removed;
        PublishRemoved(i, slot, item, removed);
      }

      return remaining == 0;
    }

    public bool RemoveItem(string itemId, int quantity = 1) {
      return RemoveItem(itemId, quantity, out _);
    }

    public bool RemoveItem(string itemId, int quantity, out int removedAmount) {
      removedAmount = 0;
      if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) {
        return false;
      }

      int remaining = quantity;

      for (int i = 0; i < _slots.Count && remaining > 0; i++) {
        InventorySlot slot = _slots[i];
        if (slot.IsEmpty || slot.Item == null || slot.Item.ItemId != itemId) {
          continue;
        }

        ItemScriptableObject item = slot.Item;
        int removed = slot.Remove(remaining);
        if (removed <= 0) {
          continue;
        }

        remaining -= removed;
        removedAmount += removed;
        PublishRemoved(i, slot, item, removed);
      }

      return remaining == 0;
    }

    public bool HasItem(ItemScriptableObject item, int quantity = 1) {
      return item != null && HasItem(item.ItemId, quantity);
    }

    public bool HasItem(string itemId, int quantity = 1) {
      return !string.IsNullOrWhiteSpace(itemId) && quantity > 0 && GetItemCount(itemId) >= quantity;
    }

    public int GetItemCount(ItemScriptableObject item) {
      return item == null ? 0 : GetItemCount(item.ItemId);
    }

    public int GetItemCount(string itemId) {
      if (string.IsNullOrWhiteSpace(itemId)) {
        return 0;
      }

      int total = 0;
      for (int i = 0; i < _slots.Count; i++) {
        InventorySlot slot = _slots[i];
        if (slot.IsEmpty || slot.Item == null) {
          continue;
        }

        if (slot.Item.ItemId == itemId) {
          total += slot.Quantity;
        }
      }

      return total;
    }

    public int FindFirstSlotIndex(string itemId) {
      if (string.IsNullOrWhiteSpace(itemId)) {
        return -1;
      }

      for (int i = 0; i < _slots.Count; i++) {
        InventorySlot slot = _slots[i];
        if (!slot.IsEmpty && slot.Item != null && slot.Item.ItemId == itemId) {
          return i;
        }
      }

      return -1;
    }

    public int FindEmptySlotIndex() {
      for (int i = 0; i < _slots.Count; i++) {
        if (_slots[i].IsEmpty) {
          return i;
        }
      }

      return -1;
    }

    public InventorySlot GetSlot(int index) {
      return index < 0 || index >= _slots.Count ? throw new ArgumentOutOfRangeException(nameof(index)) : _slots[index];
    }

    public void Load(IEnumerable<ItemStackData> stacks, bool suppressNotifications = false) {
      Clear(suppressNotifications: suppressNotifications);

      if (stacks == null) {
        return;
      }

      foreach (ItemStackData stack in stacks) {
        if (stack == null || stack.Item == null || stack.Quantity <= 0) {
          continue;
        }

        if (suppressNotifications) {
          AddSilently(stack.Item, stack.Quantity);
          continue;
        }

        _ = AddItem(stack.Item, stack.Quantity);
      }
    }

    public IEnumerable<ItemStackData> ToItemStacks() {
      for (int i = 0; i < _slots.Count; i++) {
        ItemStackData stack = _slots[i].ToStackData();
        if (stack != null) {
          yield return stack;
        }
      }
    }

    public void Clear(bool suppressNotifications = false) {
      for (int i = 0; i < _slots.Count; i++) {
        InventorySlot slot = _slots[i];
        if (slot.IsEmpty) {
          continue;
        }

        ItemScriptableObject item = slot.Item;
        int quantity = slot.Quantity;
        slot.Clear();

        if (!suppressNotifications && item != null && quantity > 0) {
          PublishRemoved(i, slot, item, quantity);
        }
      }
    }

    public void Sort(Comparison<InventorySlot> comparison = null, bool suppressNotifications = false) {
      comparison ??= DefaultSortComparison;
      _slots.Sort(comparison);

      if (suppressNotifications) {
        return;
      }

      for (int i = 0; i < _slots.Count; i++) {
        OnSlotChanged?.Invoke(i, _slots[i]);
      }
    }

    private static int DefaultSortComparison(InventorySlot left, InventorySlot right) {
      if (left == null || right == null) {
        return 0;
      }

      bool leftEmpty = left.IsEmpty;
      bool rightEmpty = right.IsEmpty;

      if (leftEmpty && rightEmpty) {
        return 0;
      }

      if (leftEmpty) {
        return 1;
      }

      if (rightEmpty) {
        return -1;
      }

      int typeComparison = left.Item.ItemType.CompareTo(right.Item.ItemType);
      return typeComparison != 0
        ? typeComparison
        : string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private void AddSilently(ItemScriptableObject item, int quantity) {
      if (item == null || quantity <= 0) {
        return;
      }

      int remaining = quantity;
      int maxStack = Mathf.Max(1, item.MaxStackSize);

      if (maxStack > 1) {
        for (int i = 0; i < _slots.Count && remaining > 0; i++) {
          InventorySlot slot = _slots[i];
          if (!slot.CanStack(item)) {
            continue;
          }

          int added = slot.Add(item, remaining);
          if (added <= 0) {
            continue;
          }

          remaining -= added;
        }
      }

      for (int i = 0; i < _slots.Count && remaining > 0; i++) {
        InventorySlot slot = _slots[i];
        if (!slot.IsEmpty) {
          continue;
        }

        int added = slot.Add(item, remaining);
        if (added <= 0) {
          continue;
        }

        remaining -= added;
      }

      if (remaining > 0) {
        Debug.LogWarning($"ItemInventory.AddSilently could not fit entire stack for {item.DisplayName} ({quantity - remaining}/{quantity}).");
      }
    }

    private void PublishAdded(int slotIndex, InventorySlot slot, ItemScriptableObject item, int amount) {
      if (amount <= 0) {
        return;
      }

      OnSlotChanged?.Invoke(slotIndex, slot);
      OnItemAdded?.Invoke(item, amount);
    }

    private void PublishRemoved(int slotIndex, InventorySlot slot, ItemScriptableObject item, int amount) {
      if (amount <= 0) {
        return;
      }

      OnSlotChanged?.Invoke(slotIndex, slot);
      OnItemRemoved?.Invoke(item, amount);
    }
  }
}
