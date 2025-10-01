using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Results;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;

namespace EchoesOfTheVoid.Core.Inventory.Player {
  [DisallowMultipleComponent]
  [RequireComponent(typeof(PlayerInventory))]
  public class PlayerEquipment : MonoBehaviour {
    [Header("Equipment Settings")]
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private List<EquipmentSlotType> _slotLayout = new();
    [SerializeField] private List<EquippedItemData> _startingEquipment = new();

    private EquipmentSet _equipment;
    private bool _isInitialized;

    public EquipmentSet Equipment {
      get {
        EnsureInitialized();
        return _equipment;
      }
    }

    public bool HasInventory => _inventory != null;

    public event Action<EquipmentSlotType, EquipmentItemScriptableObject> OnEquipped;
    public event Action<EquipmentSlotType, EquipmentItemScriptableObject> OnUnequipped;
    public event Action OnLoadoutChanged;

    private void Awake() {
      ResolveInventory();
      EnsureInitialized();
    }

    private void OnValidate() {
      ResolveInventory();
    }

    public void ApplyStartingEquipment(bool adjustInventory = true, bool notify = false) {
      if (_startingEquipment == null || _startingEquipment.Count == 0) {
        return;
      }

      LoadFromSnapshot(_startingEquipment, adjustInventory, notify);
    }

    public ItemResult TryEquip(EquipmentItemScriptableObject item, EquipmentSlotType? slotOverride = null) {
      EnsureInitialized();

      if (item == null) {
        return ItemResult.Failed("Item not set");
      }

      if (item.ItemType != ItemType.Equipment) {
        return ItemResult.Failed("Item is not equipment");
      }

      if (_inventory == null) {
        return ItemResult.Failed("No player inventory assigned");
      }

      EquipmentSlotType slotType = slotOverride ?? item.Slot;
      if (!_equipment.HasSlot(slotType)) {
        return ItemResult.Failed($"Slot {slotType} is not available");
      }

      if (!_inventory.HasItem(item)) {
        return ItemResult.Failed($"{item.DisplayName} is not available");
      }

      if (!_equipment.TryEquip(item, slotType, out List<EquipmentDisplacement> displaced)) {
        return ItemResult.Failed($"Could not equip {item.DisplayName}");
      }

      if (!_inventory.RemoveItem(item, 1)) {
        RestoreEquipment(slotType, displaced);
        return ItemResult.Failed($"Could not remove {item.DisplayName} from inventory");
      }

      var storedDisplaced = new List<EquipmentItemScriptableObject>();
      foreach (EquipmentDisplacement entry in displaced) {
        if (!_inventory.AddItem(entry.Item, 1)) {
          foreach (EquipmentItemScriptableObject stored in storedDisplaced) {
            _ = _inventory.RemoveItem(stored, 1);
          }

          _ = _inventory.AddItem(item, 1);
          RestoreEquipment(slotType, displaced);
          return ItemResult.Failed($"Inventory full for {entry.Item.DisplayName}");
        }

        storedDisplaced.Add(entry.Item);
        PublishUnequipped(entry.Slot, entry.Item, raiseLoadoutChanged: false);
      }

      PublishEquipped(slotType, item, raiseLoadoutChanged: false);
      OnLoadoutChanged?.Invoke();
      return ItemResult.Success($"{item.DisplayName} equipped");
    }

    public ItemResult TryUnequip(EquipmentSlotType slotType) {
      EnsureInitialized();

      if (!_equipment.TryUnequip(slotType, out EquipmentDisplacement displacement) || displacement.Item == null) {
        return ItemResult.Failed($"Nothing equipped in {slotType}");
      }

      if (_inventory == null) {
        _ = _equipment.TryEquip(displacement.Item, displacement.Slot, out _);
        return ItemResult.Failed("No player inventory assigned");
      }

      if (!_inventory.AddItem(displacement.Item, 1)) {
        _ = _equipment.TryEquip(displacement.Item, displacement.Slot, out _);
        return ItemResult.Failed("Inventory is full");
      }

      PublishUnequipped(displacement.Slot, displacement.Item, raiseLoadoutChanged: false);
      OnLoadoutChanged?.Invoke();
      return ItemResult.Success($"{displacement.Item.DisplayName} unequipped");
    }

    public bool TryGetEquippedItem(EquipmentSlotType slotType, out EquipmentItemScriptableObject item) {
      EnsureInitialized();
      item = _equipment.GetEquippedItem(slotType);
      return item != null;
    }

    public bool IsSlotBlocked(EquipmentSlotType slotType) {
      EnsureInitialized();
      return _equipment.IsSlotBlocked(slotType);
    }

    public List<EquippedItemData> CreateSnapshot() {
      EnsureInitialized();

      var result = new List<EquippedItemData>();
      foreach (EquipmentSlot slot in _equipment.Slots) {
        if (slot.Item == null) {
          continue;
        }

        result.Add(new EquippedItemData {
          Slot = slot.SlotType,
          Item = slot.Item
        });
      }

      return result;
    }

    public void LoadFromSnapshot(IEnumerable<EquippedItemData> loadout, bool adjustInventory, bool notify) {
      EnsureInitialized();

      var previouslyEquipped = new List<EquipmentDisplacement>();
      foreach (EquipmentSlot slot in _equipment.Slots) {
        if (slot.Item == null) {
          continue;
        }

        previouslyEquipped.Add(new EquipmentDisplacement(slot.SlotType, slot.Item));
      }

      if (notify) {
        foreach (EquipmentDisplacement entry in previouslyEquipped) {
          PublishUnequipped(entry.Slot, entry.Item, raiseLoadoutChanged: false);
        }
      }

      _equipment.Clear();

      if (adjustInventory && _inventory != null) {
        foreach (EquipmentDisplacement entry in previouslyEquipped) {
          _ = _inventory.AddItem(entry.Item, 1);
        }
      }

      if (loadout != null) {
        foreach (EquippedItemData entry in loadout) {
          if (entry == null || entry.Item == null) {
            continue;
          }

          EquipmentSlotType targetSlot = entry.Slot;
          if (!_equipment.HasSlot(targetSlot)) {
            targetSlot = entry.Item.Slot;
          }

          if (!_equipment.HasSlot(targetSlot)) {
            continue;
          }

          if (adjustInventory && _inventory != null) {
            if (!_inventory.HasItem(entry.Item) || !_inventory.RemoveItem(entry.Item, 1)) {
              Debug.LogWarning($"PlayerEquipment could not sync inventory for {entry.Item.DisplayName}.", this);
              continue;
            }
          }

          if (_equipment.TryEquip(entry.Item, targetSlot, out List<EquipmentDisplacement> displaced) && displaced.Count == 0) {
            if (notify) {
              PublishEquipped(targetSlot, entry.Item, raiseLoadoutChanged: false);
            }
          }
        }
      }

      if (notify) {
        OnLoadoutChanged?.Invoke();
      }
    }

    public void Clear(bool returnItemsToInventory) {
      EnsureInitialized();

      if (!returnItemsToInventory || _inventory == null) {
        foreach (EquipmentSlot slot in _equipment.Slots) {
          if (slot.Item != null) {
            PublishUnequipped(slot.SlotType, slot.Item, raiseLoadoutChanged: false);
          }
        }

        _equipment.Clear();
        OnLoadoutChanged?.Invoke();
        return;
      }

      foreach (EquipmentSlot slot in _equipment.Slots) {
        if (slot.Item == null) {
          continue;
        }

        if (!_inventory.AddItem(slot.Item, 1)) {
          Debug.LogWarning($"PlayerEquipment failed to return {slot.Item.DisplayName} to inventory.", this);
        }

        PublishUnequipped(slot.SlotType, slot.Item, raiseLoadoutChanged: false);
      }

      _equipment.Clear();
      OnLoadoutChanged?.Invoke();
    }

    private void EnsureInitialized() {
      if (_isInitialized) {
        return;
      }

      bool hasLayout = _slotLayout != null && _slotLayout.Count > 0;
      _equipment = hasLayout ? new EquipmentSet(_slotLayout) : new EquipmentSet();
      _isInitialized = true;
    }

    private void ResolveInventory() {
      if (_inventory != null) {
        return;
      }

      _inventory = GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
    }

    private void RestoreEquipment(EquipmentSlotType anchorSlot, List<EquipmentDisplacement> displaced) {
      _ = _equipment.TryUnequip(anchorSlot, out _);
      foreach (EquipmentDisplacement entry in displaced) {
        _ = _equipment.TryEquip(entry.Item, entry.Slot, out _);
      }
    }

    private void PublishEquipped(EquipmentSlotType slotType, EquipmentItemScriptableObject item, bool raiseLoadoutChanged) {
      OnEquipped?.Invoke(slotType, item);
      if (raiseLoadoutChanged) {
        OnLoadoutChanged?.Invoke();
      }
    }

    private void PublishUnequipped(EquipmentSlotType slotType, EquipmentItemScriptableObject item, bool raiseLoadoutChanged) {
      OnUnequipped?.Invoke(slotType, item);
      if (raiseLoadoutChanged) {
        OnLoadoutChanged?.Invoke();
      }
    }
  }
}





