using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Serializable wrapper for all saveable game data
  /// </summary>
  [Serializable]
  public class GameSaveData {
    public int Version = 1;
    public string LastSaved;

    public PlayerData PlayerData = new();
    public ProgressData ProgressData = new();
    public SettingsData SettingsData = new();
    public UIStateData UiStateData = new();
  }
}
