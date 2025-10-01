using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;

namespace EchoesOfTheVoid.Core.Inventory.Systems {
  public readonly struct EquipmentDisplacement {
    public EquipmentDisplacement(EquipmentSlotType slot, EquipmentItemScriptableObject item) {
      Slot = slot;
      Item = item;
    }

    public EquipmentSlotType Slot { get; }
    public EquipmentItemScriptableObject Item { get; }
  }
}
