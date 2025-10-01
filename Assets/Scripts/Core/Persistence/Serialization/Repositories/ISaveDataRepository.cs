namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Defines contract for save data operations.
  /// </summary>
  public interface ISaveDataRepository {
    void Save(GameSaveData data);
    GameSaveData Load();
    bool HasSaveFile();
    void DeleteSave();
  }
}
