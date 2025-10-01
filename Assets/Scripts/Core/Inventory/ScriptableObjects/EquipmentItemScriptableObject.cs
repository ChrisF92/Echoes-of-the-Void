using System.Collections.Generic;
using EchoesOfTheVoid.Core.Inventory.Data;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Inventory.ScriptableObjects {
  [CreateAssetMenu(fileName = "New Equipment Item", menuName = "Inventory/Equipment Item")]
  public class EquipmentItemScriptableObject : ItemScriptableObject {
    [Header("Equipment")]
    public EquipmentSlotType Slot;
    [Tooltip("If true, the item must occupy both main and off hand when equipped.")]
    public bool OccupiesBothHands;
    public List<EquipmentStatModifier> StatModifiers = new();

    private void Reset() {
      EnsureEquipmentDefaults();
    }

    private void OnEnable() {
      EnsureEquipmentDefaults();
    }

#if UNITY_EDITOR
    private void OnValidate() {
      EnsureEquipmentDefaults();
    }
#endif

    private void EnsureEquipmentDefaults() {
      ItemType = ItemType.Equipment;
      MaxStackSize = 1;
    }
  }
}
