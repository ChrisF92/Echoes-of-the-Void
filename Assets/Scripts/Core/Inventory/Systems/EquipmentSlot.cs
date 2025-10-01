using System;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Inventory.Systems {
  [Serializable]
  public class EquipmentSlot {
    [SerializeField] private EquipmentSlotType _slotType;
    [SerializeField] private EquipmentItemScriptableObject _item;

    public EquipmentSlotType SlotType => _slotType;
    public EquipmentItemScriptableObject Item => _item;
    public bool IsEmpty => _item == null;

    public EquipmentSlot() {
    }

    public EquipmentSlot(EquipmentSlotType slotType) {
      _slotType = slotType;
    }

    public bool CanAccept(EquipmentItemScriptableObject item) {
      return item == null || item.Slot == _slotType;
    }

    internal EquipmentItemScriptableObject Replace(EquipmentItemScriptableObject item) {
      EquipmentItemScriptableObject previous = _item;
      _item = item;
      return previous;
    }
  }
}
