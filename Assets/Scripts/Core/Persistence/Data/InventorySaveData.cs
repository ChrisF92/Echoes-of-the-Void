using System;
using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class InventorySaveData {
    public bool IsInitialized;
    public int Capacity = 30;
    public List<ItemStackRecord> Items = new();
  }

  [Serializable]
  public class ItemStackRecord {
    public string ItemId = string.Empty;
    public int Quantity;
  }
}
