using System.Collections.Generic;

using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;

namespace EchoesOfTheVoid.Core.Combat.Components {
  public class EquipmentComponent : CombatComponent {
    private readonly Dictionary<StatType, StatModifierValue> _cachedModifiers = new();
    private bool _suppressNotifications;

    public EquipmentSet Equipment { get; } = new();

    public event System.Action<EquipmentSlotType, EquipmentItemScriptableObject> OnEquipped;
    public event System.Action<EquipmentSlotType, EquipmentItemScriptableObject> OnUnequipped;
    public event System.Action OnModifiersChanged;

    public override void Initialize(ICombatant owner) {
      RecalculateModifiers();
    }

    public override void Update(float deltaTime) {
    }

    public bool TryGetModifier(StatType statType, out StatModifier modifier) {
      if (_cachedModifiers.TryGetValue(statType, out StatModifierValue totals)) {
        modifier = new StatModifier(totals.Additive, totals.Percent);
        return true;
      }

      modifier = default;
      return false;
    }

    public StatModifier GetModifier(StatType statType) {
      return _cachedModifiers.TryGetValue(statType, out StatModifierValue totals)
        ? new StatModifier(totals.Additive, totals.Percent)
        : default;
    }

    public IReadOnlyDictionary<StatType, StatModifierValue> GetAllModifiers() {
      return _cachedModifiers;
    }

    public bool IsSlotBlocked(EquipmentSlotType slotType) {
      return Equipment.IsSlotBlocked(slotType);
    }

    public List<EquippedItemData> CreateSnapshot() {
      var result = new List<EquippedItemData>();
      foreach (EquipmentSlot slot in Equipment.Slots) {
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

    public void LoadFromSnapshot(IEnumerable<EquippedItemData> loadout, bool suppressNotifications = true) {
      bool previousSuppression = _suppressNotifications;
      _suppressNotifications = suppressNotifications || previousSuppression;

      var previousItems = new List<EquipmentDisplacement>();
      foreach (EquipmentSlot slot in Equipment.Slots) {
        if (slot.Item == null) {
          continue;
        }

        previousItems.Add(new EquipmentDisplacement(slot.SlotType, slot.Item));
      }

      foreach (EquipmentDisplacement entry in previousItems) {
        PublishUnequipped(entry.Slot, entry.Item);
      }

      Equipment.Clear();

      if (loadout != null) {
        foreach (EquippedItemData entry in loadout) {
          if (entry == null || entry.Item == null) {
            continue;
          }

          EquipmentSlotType targetSlot = entry.Slot;
          if (!Equipment.HasSlot(targetSlot)) {
            targetSlot = entry.Item.Slot;
          }

          if (!Equipment.HasSlot(targetSlot)) {
            continue;
          }

          if (Equipment.TryEquip(entry.Item, targetSlot, out List<EquipmentDisplacement> displaced) && displaced.Count == 0) {
            PublishEquipped(targetSlot, entry.Item);
          }
        }
      }

      _suppressNotifications = previousSuppression;
      RecalculateModifiers();
    }

    public void LoadFromPlayerEquipment(PlayerEquipment playerEquipment, bool suppressNotifications = true) {
      if (playerEquipment == null) {
        LoadFromSnapshot(null, suppressNotifications);
        return;
      }

      List<EquippedItemData> snapshot = playerEquipment.CreateSnapshot();
      LoadFromSnapshot(snapshot, suppressNotifications);
    }

    private void PublishUnequipped(EquipmentSlotType slotType, EquipmentItemScriptableObject item) {
      if (_suppressNotifications) {
        return;
      }

      OnUnequipped?.Invoke(slotType, item);
    }

    private void PublishEquipped(EquipmentSlotType slotType, EquipmentItemScriptableObject item) {
      if (_suppressNotifications) {
        return;
      }

      OnEquipped?.Invoke(slotType, item);
    }

    private void RecalculateModifiers() {
      _cachedModifiers.Clear();
      foreach (EquipmentItemScriptableObject equippedItem in Equipment.GetEquippedItems()) {
        if (equippedItem == null || equippedItem.StatModifiers == null) {
          continue;
        }

        foreach (EquipmentStatModifier modifier in equippedItem.StatModifiers) {
          if (!_cachedModifiers.TryGetValue(modifier.Stat, out StatModifierValue totals)) {
            totals = new StatModifierValue();
          }

          totals.Additive += modifier.FlatBonus;
          totals.Percent += modifier.PercentBonus;
          _cachedModifiers[modifier.Stat] = totals;
        }
      }

      if (!_suppressNotifications) {
        OnModifiersChanged?.Invoke();
      }
    }

    public readonly struct StatModifier {
      public StatModifier(int additive, float percent) {
        Additive = additive;
        Percent = percent;
      }

      public int Additive { get; }
      public float Percent { get; }

      public int Apply(int baseValue) {
        int adjusted = (int)(baseValue * (1f + Percent));
        return adjusted + Additive;
      }
    }

    public struct StatModifierValue {
      public int Additive;
      public float Percent;
    }
  }
}
