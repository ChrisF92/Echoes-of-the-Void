using System;
using EchoesOfTheVoid.Core.Combat;

namespace EchoesOfTheVoid.Core.Inventory.Data {
  [Serializable]
  public struct EquipmentStatModifier {
    public StatType Stat;
    public int FlatBonus;
    public float PercentBonus;

    public void Accumulate(ref int additiveTotal, ref float percentTotal) {
      additiveTotal += FlatBonus;
      percentTotal += PercentBonus;
    }
  }
}
