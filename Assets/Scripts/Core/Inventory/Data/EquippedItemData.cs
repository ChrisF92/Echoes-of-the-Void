using System;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Inventory.Data {
  [Serializable]
  public class EquippedItemData {
    public EquipmentSlotType Slot;
    public EquipmentItemScriptableObject Item;
  }
}
