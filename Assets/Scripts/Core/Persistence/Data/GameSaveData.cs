using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Serializable wrapper for all saveable game data
  /// </summary>
  [Serializable]
  public class GameSaveData {
    public const int CurrentVersion = 2;

    public int Version = CurrentVersion;
    public string LastSaved;

    public PlayerProfileData Player = new();
    public InventorySaveData Inventory = new();
    public RosterSaveData Roster = new();
    public ProgressData Progress = new();
    public SettingsData Settings = new();
    public UIStateData UiState = new();
  }
}
