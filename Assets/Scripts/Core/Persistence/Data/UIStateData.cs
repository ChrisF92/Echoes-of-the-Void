using System;
using System.Collections.Generic;

namespace EchoesOfTheVoid.Core.Persistence {
  [Serializable]
  public class UIStateData {
    public string LastActiveScreen = "MainMenu";
    public Dictionary<string, bool> TutorialCompleted = new();
    public Dictionary<string, object> UiPreferences = new();
  }
}
