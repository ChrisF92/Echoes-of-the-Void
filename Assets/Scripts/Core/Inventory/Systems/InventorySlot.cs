using System;
using UnityEngine;

using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Inventory.Systems {
  [Serializable]
  public class InventorySlot {
    [SerializeField] private ItemScriptableObject _item;
    [SerializeField] private int _quantity;

    public ItemScriptableObject Item => _item;
    public int Quantity => _quantity;
    public bool IsEmpty => _item == null || _quantity <= 0;
    public int RemainingCapacity => IsEmpty || _item == null ? 0 : Mathf.Max(0, GetMaxStackSize() - _quantity);

    public bool CanStack(ItemScriptableObject item) {
      return !IsEmpty && _item == item && GetMaxStackSize() > 1 && _quantity < GetMaxStackSize();
    }

    public bool CanAccept(ItemScriptableObject item) {
      return item == null ? false : IsEmpty || _item == item && _quantity < GetMaxStackSize();
    }

    public int AvailableSpaceFor(ItemScriptableObject item) {
      return !CanAccept(item) ? 0 : IsEmpty ? GetMaxStackSize(item) : Mathf.Max(0, GetMaxStackSize() - _quantity);
    }

    internal int Add(ItemScriptableObject item, int amount) {
      if (item == null || amount <= 0 || !CanAccept(item)) {
        return 0;
      }

      if (IsEmpty) {
        int toAdd = Mathf.Clamp(amount, 0, GetMaxStackSize(item));
        _item = item;
        _quantity = toAdd;
        return toAdd;
      }

      int maxStack = GetMaxStackSize();
      int newQuantity = Mathf.Min(_quantity + amount, maxStack);
      int added = newQuantity - _quantity;
      _quantity = newQuantity;
      return added;
    }

    internal int Remove(int amount) {
      if (IsEmpty || amount <= 0) {
        return 0;
      }

      int removed = Mathf.Min(amount, _quantity);
      _quantity -= removed;

      if (_quantity <= 0) {
        Clear();
      }

      return removed;
    }

    internal void Set(ItemScriptableObject item, int quantity) {
      if (item == null || quantity <= 0) {
        Clear();
        return;
      }

      _item = item;
      _quantity = Mathf.Clamp(quantity, 0, GetMaxStackSize(item));
    }

    internal void Load(ItemStackData stack) {
      if (stack == null || stack.Item == null || stack.Quantity <= 0) {
        Clear();
        return;
      }

      _item = stack.Item;
      _quantity = Mathf.Clamp(stack.Quantity, 0, GetMaxStackSize());
    }

    internal ItemStackData ToStackData() {
      return IsEmpty
        ? null
        : new ItemStackData {
          Item = _item,
          Quantity = _quantity
        };
    }

    internal void Clear() {
      _item = null;
      _quantity = 0;
    }

    private int GetMaxStackSize() {
      return GetMaxStackSize(_item);
    }

    private static int GetMaxStackSize(ItemScriptableObject item) {
      return item != null ? Mathf.Max(1, item.MaxStackSize) : 0;
    }
  }
}
