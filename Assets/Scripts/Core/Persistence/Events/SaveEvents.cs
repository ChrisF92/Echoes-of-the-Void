using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Centralized save-related events.
  /// </summary>
  public static class SaveEvents {
    public static event EventHandler<SaveDataEventArgs> DataLoaded;
    public static event EventHandler<SaveDataEventArgs> DataSaved;
    public static event EventHandler<SaveErrorEventArgs> SaveError;

    internal static void RaiseDataLoaded(GameSaveData data) {
      DataLoaded?.Invoke(null, new SaveDataEventArgs(data));
    }

    internal static void RaiseDataSaved(GameSaveData data) {
      DataSaved?.Invoke(null, new SaveDataEventArgs(data));
    }

    internal static void RaiseSaveError(string message, Exception ex = null) {
      SaveError?.Invoke(null, new SaveErrorEventArgs(message, ex));
    }
  }
}
