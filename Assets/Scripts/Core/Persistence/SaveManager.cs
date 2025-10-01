using System.IO;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Manages game save operations and coordinates between repository and game state.
  /// </summary>
  public class SaveManager : MonoBehaviour {
    public static SaveManager Instance { get; private set; }

    [Header("Save Configuration")]
    [SerializeField] private string _saveFileName = "gamesave.dat";
    [SerializeField] private bool _useEncryption = true;
    [SerializeField] private string _encryptionKey = "ChangeThisKey123!";

    [Header("Auto-Save Settings")]
    [SerializeField] private bool _autoSaveEnabled = true;
    [SerializeField] private float _autoSaveDelay = 2f;

    private ISaveDataRepository _repository;
    private GameSaveData _currentSaveData;
    private bool _isDirty;
    private float _lastChangeTime;

    private void Awake() {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeSaveSystem();
      } else {
        Destroy(gameObject);
      }
    }

    private void Start() {
      LoadGame();
      SubscribeToGameStateEvents();
    }

    private void Update() {
      if (_autoSaveEnabled && _isDirty && Time.time - _lastChangeTime >= _autoSaveDelay) {
        SaveGame();
        _isDirty = false;
      }
    }

    private void InitializeSaveSystem() {
      string saveFilePath = Path.Combine(Application.persistentDataPath, _saveFileName);

      ISaveSerializer serializer = new JsonSaveSerializer(prettyPrint: false);
      IFileManager fileManager = new StandardFileManager();
      IEncryptionProvider encryptionProvider = _useEncryption
          ? new XorEncryptionProvider(_encryptionKey)
          : null;

      _repository = new SaveDataRepository(
          saveFilePath,
          serializer,
          fileManager,
          encryptionProvider,
          _useEncryption);

      _currentSaveData = new GameSaveData();

      Debug.Log($"[SaveManager] Initialized. Save path: {saveFilePath}");
    }

    private void SubscribeToGameStateEvents() {
      // Player events
      GameStateEvents.PlayerNameChanged += name => {
        _currentSaveData.PlayerData.PlayerName = name;
        MarkDirty();
      };

      GameStateEvents.PlayerLevelChanged += level => {
        _currentSaveData.PlayerData.Level = level;
        MarkDirty();
      };

      GameStateEvents.ExperienceChanged += exp => {
        _currentSaveData.PlayerData.Experience = exp;
        MarkDirty();
      };

      GameStateEvents.CurrencyChanged += currency => {
        _currentSaveData.PlayerData.Currency = currency;
        MarkDirty();
      };

      GameStateEvents.ItemAdded += item => {
        if (!_currentSaveData.PlayerData.Inventory.Contains(item)) {
          _currentSaveData.PlayerData.Inventory.Add(item);
          MarkDirty();
        }
      };

      GameStateEvents.ItemRemoved += item => {
        if (_currentSaveData.PlayerData.Inventory.Remove(item)) {
          MarkDirty();
        }
      };

      GameStateEvents.StatChanged += (stat, value) => {
        _currentSaveData.PlayerData.Stats[stat] = value;
        MarkDirty();
      };

      // Progress events
      GameStateEvents.QuestCompleted += questId => {
        if (!_currentSaveData.ProgressData.CompletedQuests.Contains(questId)) {
          _currentSaveData.ProgressData.CompletedQuests.Add(questId);
          MarkDirty();
        }
      };

      GameStateEvents.FeatureUnlocked += (feature, unlocked) => {
        _currentSaveData.ProgressData.UnlockedFeatures[feature] = unlocked;
        MarkDirty();
      };

      GameStateEvents.GameFlagChanged += (flag, value) => {
        _currentSaveData.ProgressData.GameFlags[flag] = value;
        MarkDirty();
      };

      GameStateEvents.LevelProgressed += level => {
        _currentSaveData.ProgressData.CurrentLevel = level;
        MarkDirty();
      };

      // Settings events
      GameStateEvents.VolumeChanged += volume => {
        _currentSaveData.SettingsData.MasterVolume = volume;
        MarkDirty();
      };

      GameStateEvents.NotificationsToggled += enabled => {
        _currentSaveData.SettingsData.NotificationsEnabled = enabled;
        MarkDirty();
      };

      GameStateEvents.LanguageChanged += language => {
        _currentSaveData.SettingsData.Language = language;
        MarkDirty();
      };

      GameStateEvents.CustomSettingChanged += (key, value) => {
        _currentSaveData.SettingsData.CustomSettings[key] = value;
        MarkDirty();
      };

      // UI events
      GameStateEvents.ScreenChanged += screenName => {
        _currentSaveData.UiStateData.LastActiveScreen = screenName;
        MarkDirty();
      };

      GameStateEvents.TutorialCompleted += (tutorialId, completed) => {
        _currentSaveData.UiStateData.TutorialCompleted[tutorialId] = completed;
        MarkDirty();
      };

      GameStateEvents.UIPreferenceChanged += (key, value) => {
        _currentSaveData.UiStateData.UiPreferences[key] = value;
        MarkDirty();
      };

      // System events
      GameStateEvents.SaveRequested += SaveGame;
      GameStateEvents.LoadRequested += LoadGame;
    }

    private void MarkDirty() {
      _isDirty = true;
      _lastChangeTime = Time.time;
    }

    public void SaveGame() {
      _repository.Save(_currentSaveData);
    }

    public void LoadGame() {
      _currentSaveData = _repository.Load();
    }

    public GameSaveData GetCurrentSaveData() => _currentSaveData;
    public PlayerData GetPlayerData() => _currentSaveData.PlayerData;
    public ProgressData GetProgressData() => _currentSaveData.ProgressData;
    public SettingsData GetSettingsData() => _currentSaveData.SettingsData;
    public UIStateData GetUIStateData() => _currentSaveData.UiStateData;

    public bool HasSaveFile() => _repository.HasSaveFile();
    public void DeleteSaveFile() => _repository.DeleteSave();

    public void ResetToDefaults() {
      _currentSaveData = new GameSaveData();
      SaveGame();
    }

    private void OnApplicationPause(bool pauseStatus) {
      if (pauseStatus) {
        SaveGame();
      }
    }

    private void OnApplicationFocus(bool hasFocus) {
      if (!hasFocus) {
        SaveGame();
      }
    }

    private void OnDestroy() {
      SaveGame();
    }
  }
}
