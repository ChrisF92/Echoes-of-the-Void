using System;
using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class ProgressData {
    public int CurrentLevel = 1;
    public List<string> CompletedQuests = new();
    public Dictionary<string, bool> UnlockedFeatures = new();
    public Dictionary<string, object> GameFlags = new();
  }
}
