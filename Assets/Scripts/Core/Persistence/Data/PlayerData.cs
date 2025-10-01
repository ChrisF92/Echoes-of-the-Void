using System;
using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class PlayerData {
    public string PlayerName = "";
    public int Level = 1;
    public int Experience = 0;
    public int Currency = 0;
    public List<string> Inventory = new();
    public Dictionary<string, int> Stats = new();
  }
}
