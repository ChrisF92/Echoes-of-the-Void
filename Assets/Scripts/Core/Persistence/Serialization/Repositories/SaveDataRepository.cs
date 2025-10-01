using System;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Repository for save data operations (Repository Pattern).
  /// </summary>
  public class SaveDataRepository : ISaveDataRepository {
    private readonly ISaveSerializer _serializer;
    private readonly IEncryptionProvider _encryptionProvider;
    private readonly IFileManager _fileManager;
    private readonly string _saveFilePath;
    private readonly bool _useEncryption;

    public SaveDataRepository(
        string saveFilePath,
        ISaveSerializer serializer,
        IFileManager fileManager,
        IEncryptionProvider encryptionProvider = null,
        bool useEncryption = false) {
      _saveFilePath = saveFilePath ?? throw new ArgumentNullException(nameof(saveFilePath));
      _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
      _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
      _encryptionProvider = encryptionProvider;
      _useEncryption = useEncryption && encryptionProvider != null;
    }

    public void Save(GameSaveData data) {
      try {
        data.LastSaved = DateTime.Now.ToString("o"); // ISO 8601 format

        string serializedData = _serializer.Serialize(data);

        if (_useEncryption) {
          serializedData = _encryptionProvider.Encrypt(serializedData);
        }

        _fileManager.Write(_saveFilePath, serializedData);
        SaveEvents.RaiseDataSaved(data);

        Debug.Log($"[SaveDataRepository] Save successful: {_saveFilePath}");
      } catch (Exception ex) {
        string errorMessage = $"Failed to save data: {ex.Message}";
        Debug.LogError($"[SaveDataRepository] {errorMessage}");
        SaveEvents.RaiseSaveError(errorMessage, ex);
        throw;
      }
    }

    public GameSaveData Load() {
      try {
        if (!_fileManager.Exists(_saveFilePath)) {
          Debug.Log("[SaveDataRepository] No save file found, creating new data");
          return new GameSaveData();
        }

        string fileContent = _fileManager.Read(_saveFilePath);

        if (_useEncryption) {
          fileContent = _encryptionProvider.Decrypt(fileContent);
        }

        GameSaveData data = _serializer.Deserialize<GameSaveData>(fileContent);
        SaveEvents.RaiseDataLoaded(data);

        Debug.Log($"[SaveDataRepository] Load successful: {_saveFilePath}");
        return data;
      } catch (Exception ex) {
        string errorMessage = $"Failed to load data: {ex.Message}";
        Debug.LogError($"[SaveDataRepository] {errorMessage}");
        SaveEvents.RaiseSaveError(errorMessage, ex);

        // Return new data as fallback
        return new GameSaveData();
      }
    }

    public bool HasSaveFile() {
      return _fileManager.Exists(_saveFilePath);
    }

    public void DeleteSave() {
      try {
        if (_fileManager.Exists(_saveFilePath)) {
          _fileManager.Delete(_saveFilePath);
          Debug.Log("[SaveDataRepository] Save file deleted");
        }
      } catch (Exception ex) {
        Debug.LogError($"[SaveDataRepository] Failed to delete save file: {ex.Message}");
        throw;
      }
    }
  }
}
