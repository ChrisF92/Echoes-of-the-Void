using System;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class PlayerProfileData {
    public string PlayerName = string.Empty;
    public int Level = 1;
    public int Experience = 0;
    public int Currency = 0;
  }
}
