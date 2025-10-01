using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Event arguments for save data changes.
  /// </summary>
  public class SaveDataEventArgs : EventArgs {
    public GameSaveData Data { get; }

    public SaveDataEventArgs(GameSaveData data) {
      Data = data;
    }
  }
}
