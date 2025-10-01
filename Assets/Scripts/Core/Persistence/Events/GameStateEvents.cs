using System;

namespace EchoesOfTheVoid.Core.Persistence {
  /// <summary>
  /// Game state change events for triggering saves.
  /// </summary>
  public static class GameStateEvents {
    // Player events
    public static event Action<string> PlayerNameChanged;
    public static event Action<int> PlayerLevelChanged;
    public static event Action<int> ExperienceChanged;
    public static event Action<int> CurrencyChanged;
    public static event Action<string> ItemAdded;
    public static event Action<string> ItemRemoved;
    public static event Action<string, int> StatChanged;

    // Progress events
    public static event Action<string> QuestCompleted;
    public static event Action<string, bool> FeatureUnlocked;
    public static event Action<string, object> GameFlagChanged;
    public static event Action<int> LevelProgressed;

    // Settings events
    public static event Action<float> VolumeChanged;
    public static event Action<bool> NotificationsToggled;
    public static event Action<string> LanguageChanged;
    public static event Action<string, object> CustomSettingChanged;

    // UI events
    public static event Action<string> ScreenChanged;
    public static event Action<string, bool> TutorialCompleted;
    public static event Action<string, object> UIPreferenceChanged;

    // System events
    public static event Action SaveRequested;
    public static event Action LoadRequested;
  }
}
