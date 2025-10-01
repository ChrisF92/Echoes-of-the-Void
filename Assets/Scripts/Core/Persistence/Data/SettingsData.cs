using System;
using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class SettingsData {
    public float MasterVolume = 1.0f;
    public bool NotificationsEnabled = true;
    public string Language = "en";
    public Dictionary<string, object> CustomSettings = new();
  }
}
