using System.IO;
using EchoesOfTheVoid.Core.Combat.Database;
using EchoesOfTheVoid.Core.Inventory.Database;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Systems;
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

    [Header("Runtime References")]
    [SerializeField] private PlayerProfileService _profileService;
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private PlayerRosterService _rosterService;
    [SerializeField] private ItemDatabase _itemDatabase;
    [SerializeField] private CombatantDatabase _combatantDatabase;

    private ISaveDataRepository _repository;
    private GameSaveData _currentSaveData;
    private SaveDataSynchronizer _synchronizer;

    private bool _isDirty;
    private float _lastChangeTime;
    private bool _runtimeEventsSubscribed;
    private bool _gameStateEventsSubscribed;

    private void Awake() {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeSaveSystem();
      } else if (Instance != this) {
        Destroy(gameObject);
      }
    }

    private void Start() {
      ResolveDependencies();
      InitializeSynchronizer();
      LoadGame();
      SubscribeRuntimeEvents();
      SubscribeGameStateEvents();
    }

    private void Update() {
      if (_autoSaveEnabled && _isDirty && Time.time - _lastChangeTime >= _autoSaveDelay) {
        SaveGame();
      }
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
      if (Instance == this) {
        UnsubscribeRuntimeEvents();
        UnsubscribeGameStateEvents();
        SaveGame();
        Instance = null;
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

    private void ResolveDependencies() {
      _profileService ??= PlayerProfileService.Instance ?? FindFirstObjectByType<PlayerProfileService>();
      _playerInventory ??= FindFirstObjectByType<PlayerInventory>();
      _rosterService ??= FindFirstObjectByType<PlayerRosterService>();
      _itemDatabase ??= ItemDatabase.Instance ?? FindFirstObjectByType<ItemDatabase>();
      _combatantDatabase ??= CombatantDatabase.Instance ?? FindFirstObjectByType<CombatantDatabase>();
    }

    private void InitializeSynchronizer() {
      ResolveDependencies();
      _synchronizer = new SaveDataSynchronizer(
        _profileService,
        _rosterService,
        _playerInventory,
        _itemDatabase,
        _combatantDatabase);
    }

    private void SubscribeRuntimeEvents() {
      if (_runtimeEventsSubscribed) {
        return;
      }

      if (_profileService != null) {
        _profileService.OnPlayerNameChanged += HandleProfileChanged;
        _profileService.OnLevelChanged += HandleProfileChanged;
        _profileService.OnExperienceChanged += HandleProfileChanged;
        _profileService.OnCurrencyChanged += HandleProfileChanged;
      }

      if (_playerInventory != null) {
        _playerInventory.OnSlotChanged += HandleInventoryChanged;
        _playerInventory.OnItemAdded += HandleInventoryChanged;
        _playerInventory.OnItemRemoved += HandleInventoryChanged;
      }

      if (_rosterService != null) {
        _rosterService.OnRosterChanged += HandleRosterChanged;
      }

      _runtimeEventsSubscribed = true;
    }

    private void UnsubscribeRuntimeEvents() {
      if (!_runtimeEventsSubscribed) {
        return;
      }

      if (_profileService != null) {
        _profileService.OnPlayerNameChanged -= HandleProfileChanged;
        _profileService.OnLevelChanged -= HandleProfileChanged;
        _profileService.OnExperienceChanged -= HandleProfileChanged;
        _profileService.OnCurrencyChanged -= HandleProfileChanged;
      }

      if (_playerInventory != null) {
        _playerInventory.OnSlotChanged -= HandleInventoryChanged;
        _playerInventory.OnItemAdded -= HandleInventoryChanged;
        _playerInventory.OnItemRemoved -= HandleInventoryChanged;
      }

      if (_rosterService != null) {
        _rosterService.OnRosterChanged -= HandleRosterChanged;
      }

      _runtimeEventsSubscribed = false;
    }

    private void SubscribeGameStateEvents() {
      if (_gameStateEventsSubscribed) {
        return;
      }

      // Player profile fallbacks for legacy broadcasts.
      GameStateEvents.PlayerNameChanged += HandlePlayerNameEvent;
      GameStateEvents.PlayerLevelChanged += HandlePlayerLevelEvent;
      GameStateEvents.ExperienceChanged += HandlePlayerExperienceEvent;
      GameStateEvents.CurrencyChanged += HandlePlayerCurrencyEvent;
      GameStateEvents.ItemAdded += HandleLegacyInventoryChanged;
      GameStateEvents.ItemRemoved += HandleLegacyInventoryChanged;
      GameStateEvents.StatChanged += HandleLegacyStatChanged;

      // Progress events.
      GameStateEvents.QuestCompleted += HandleQuestCompleted;
      GameStateEvents.FeatureUnlocked += HandleFeatureUnlocked;
      GameStateEvents.GameFlagChanged += HandleGameFlagChanged;
      GameStateEvents.LevelProgressed += HandleProgressLevel;

      // Settings events.
      GameStateEvents.VolumeChanged += HandleVolumeChanged;
      GameStateEvents.NotificationsToggled += HandleNotificationsToggled;
      GameStateEvents.LanguageChanged += HandleLanguageChanged;
      GameStateEvents.CustomSettingChanged += HandleCustomSettingChanged;

      // UI events.
      GameStateEvents.ScreenChanged += HandleScreenChanged;
      GameStateEvents.TutorialCompleted += HandleTutorialCompleted;
      GameStateEvents.UIPreferenceChanged += HandleUiPreferenceChanged;

      GameStateEvents.SaveRequested += SaveGame;
      GameStateEvents.LoadRequested += LoadGame;

      _gameStateEventsSubscribed = true;
    }

    private void UnsubscribeGameStateEvents() {
      if (!_gameStateEventsSubscribed) {
        return;
      }

      GameStateEvents.PlayerNameChanged -= HandlePlayerNameEvent;
      GameStateEvents.PlayerLevelChanged -= HandlePlayerLevelEvent;
      GameStateEvents.ExperienceChanged -= HandlePlayerExperienceEvent;
      GameStateEvents.CurrencyChanged -= HandlePlayerCurrencyEvent;
      GameStateEvents.ItemAdded -= HandleLegacyInventoryChanged;
      GameStateEvents.ItemRemoved -= HandleLegacyInventoryChanged;
      GameStateEvents.StatChanged -= HandleLegacyStatChanged;

      GameStateEvents.QuestCompleted -= HandleQuestCompleted;
      GameStateEvents.FeatureUnlocked -= HandleFeatureUnlocked;
      GameStateEvents.GameFlagChanged -= HandleGameFlagChanged;
      GameStateEvents.LevelProgressed -= HandleProgressLevel;

      GameStateEvents.VolumeChanged -= HandleVolumeChanged;
      GameStateEvents.NotificationsToggled -= HandleNotificationsToggled;
      GameStateEvents.LanguageChanged -= HandleLanguageChanged;
      GameStateEvents.CustomSettingChanged -= HandleCustomSettingChanged;

      GameStateEvents.ScreenChanged -= HandleScreenChanged;
      GameStateEvents.TutorialCompleted -= HandleTutorialCompleted;
      GameStateEvents.UIPreferenceChanged -= HandleUiPreferenceChanged;

      GameStateEvents.SaveRequested -= SaveGame;
      GameStateEvents.LoadRequested -= LoadGame;

      _gameStateEventsSubscribed = false;
    }

    private void HandleProfileChanged(string _) {
      MarkDirty();
    }

    private void HandleProfileChanged(int _) {
      MarkDirty();
    }

    private void HandleInventoryChanged(int _, InventorySlot __) {
      MarkDirty();
    }

    private void HandleInventoryChanged(ItemScriptableObject _, int __) {
      MarkDirty();
    }

    private void HandleRosterChanged() {
      MarkDirty();
    }

    private void HandlePlayerNameEvent(string playerName) {
      _profileService?.SetPlayerName(playerName);
    }

    private void HandlePlayerLevelEvent(int level) {
      _profileService?.SetLevel(level);
    }

    private void HandlePlayerExperienceEvent(int experience) {
      _profileService?.SetExperience(experience);
    }

    private void HandlePlayerCurrencyEvent(int currency) {
      _profileService?.SetCurrency(currency);
    }

    private void HandleLegacyInventoryChanged(string _) {
      MarkDirty();
    }

    private void HandleLegacyStatChanged(string _, int __) {
      MarkDirty();
    }

    private void HandleQuestCompleted(string questId) {
      if (string.IsNullOrWhiteSpace(questId)) {
        return;
      }

      if (!_currentSaveData.Progress.CompletedQuests.Contains(questId)) {
        _currentSaveData.Progress.CompletedQuests.Add(questId);
        MarkDirty();
      }
    }

    private void HandleFeatureUnlocked(string feature, bool unlocked) {
      if (string.IsNullOrWhiteSpace(feature)) {
        return;
      }

      _currentSaveData.Progress.UnlockedFeatures[feature] = unlocked;
      MarkDirty();
    }

    private void HandleGameFlagChanged(string flag, object value) {
      if (string.IsNullOrWhiteSpace(flag)) {
        return;
      }

      _currentSaveData.Progress.GameFlags[flag] = value;
      MarkDirty();
    }

    private void HandleProgressLevel(int level) {
      _currentSaveData.Progress.CurrentLevel = Mathf.Max(1, level);
      MarkDirty();
    }

    private void HandleVolumeChanged(float volume) {
      _currentSaveData.Settings.MasterVolume = Mathf.Clamp01(volume);
      MarkDirty();
    }

    private void HandleNotificationsToggled(bool enabled) {
      _currentSaveData.Settings.NotificationsEnabled = enabled;
      MarkDirty();
    }

    private void HandleLanguageChanged(string language) {
      if (string.IsNullOrWhiteSpace(language)) {
        return;
      }

      _currentSaveData.Settings.Language = language;
      MarkDirty();
    }

    private void HandleCustomSettingChanged(string key, object value) {
      if (string.IsNullOrWhiteSpace(key)) {
        return;
      }

      _currentSaveData.Settings.CustomSettings[key] = value;
      MarkDirty();
    }

    private void HandleScreenChanged(string screenId) {
      if (string.IsNullOrWhiteSpace(screenId)) {
        return;
      }

      _currentSaveData.UiState.LastActiveScreen = screenId;
      MarkDirty();
    }

    private void HandleTutorialCompleted(string tutorialId, bool completed) {
      if (string.IsNullOrWhiteSpace(tutorialId)) {
        return;
      }

      _currentSaveData.UiState.TutorialCompleted[tutorialId] = completed;
      MarkDirty();
    }

    private void HandleUiPreferenceChanged(string key, object value) {
      if (string.IsNullOrWhiteSpace(key)) {
        return;
      }

      _currentSaveData.UiState.UiPreferences[key] = value;
      MarkDirty();
    }

    private void MarkDirty() {
      _isDirty = true;
      _lastChangeTime = Time.time;
    }

    public void RequestSave() {
      MarkDirty();
    }

    public void SaveGame() {
      if (_repository == null || _currentSaveData == null) {
        return;
      }

      _synchronizer?.Capture(_currentSaveData);
      UpgradeSaveData(_currentSaveData);
      _repository.Save(_currentSaveData);
      _isDirty = false;
    }

    public void LoadGame() {
      if (_repository == null) {
        InitializeSaveSystem();
      }

      _currentSaveData = _repository.Load() ?? new GameSaveData();
      UpgradeSaveData(_currentSaveData);
      _synchronizer?.Apply(_currentSaveData);
      _isDirty = false;
    }

    private static void UpgradeSaveData(GameSaveData data) {
      if (data == null) {
        return;
      }

      data.Version = GameSaveData.CurrentVersion;
      data.Player ??= new PlayerProfileData();
      data.Inventory ??= new InventorySaveData();
      data.Roster ??= new RosterSaveData();
      data.Progress ??= new ProgressData();
      data.Settings ??= new SettingsData();
      data.UiState ??= new UIStateData();
    }

    public GameSaveData GetCurrentSaveData() => _currentSaveData;
    public PlayerProfileData GetPlayerProfile() => _currentSaveData.Player;
    public InventorySaveData GetInventoryData() => _currentSaveData.Inventory;
    public RosterSaveData GetRosterData() => _currentSaveData.Roster;
    public ProgressData GetProgressData() => _currentSaveData.Progress;
    public SettingsData GetSettingsData() => _currentSaveData.Settings;
    public UIStateData GetUiStateData() => _currentSaveData.UiState;

    public bool HasSaveFile() => _repository?.HasSaveFile() ?? false;
    public void DeleteSaveFile() => _repository?.DeleteSave();

    public void ResetToDefaults() {
      _currentSaveData = new GameSaveData();
      _synchronizer?.Apply(_currentSaveData);
      MarkDirty();
      SaveGame();
    }
  }
}
