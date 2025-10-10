using System;
using UnityEngine;

using EchoesOfTheVoid.Core.Persistence;

namespace EchoesOfTheVoid.Core.Systems {
  [DisallowMultipleComponent]
  public class PlayerProfileService : MonoBehaviour {
    public static PlayerProfileService Instance { get; private set; }

    [Header("Profile")]
    [SerializeField] private string _playerName = "Player";
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField, Min(0)] private int _experience;
    [SerializeField, Min(0)] private int _currency;

    public event Action<string> OnPlayerNameChanged;
    public event Action<int> OnLevelChanged;
    public event Action<int> OnExperienceChanged;
    public event Action<int> OnCurrencyChanged;

    public string PlayerName => _playerName;
    public int Level => _level;
    public int Experience => _experience;
    public int Currency => _currency;

    private void Awake() {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
      } else if (Instance != this) {
        Destroy(gameObject);
      }
    }

    public PlayerProfileData CreateSnapshot() {
      return new PlayerProfileData {
        PlayerName = _playerName,
        Level = _level,
        Experience = _experience,
        Currency = _currency
      };
    }

    public void ApplySnapshot(PlayerProfileData data, bool suppressEvents = false) {
      if (data == null) {
        return;
      }

      SetPlayerName(data.PlayerName, suppressEvents);
      SetLevel(Mathf.Max(1, data.Level), suppressEvents);
      SetExperience(Mathf.Max(0, data.Experience), suppressEvents);
      SetCurrency(Mathf.Max(0, data.Currency), suppressEvents);
    }

    public void SetPlayerName(string value, bool suppressEvents = false) {
      value ??= string.Empty;
      if (string.Equals(_playerName, value, StringComparison.Ordinal)) {
        return;
      }

      _playerName = value;
      if (!suppressEvents) {
        OnPlayerNameChanged?.Invoke(_playerName);
      }
    }

    public void SetLevel(int value, bool suppressEvents = false) {
      value = Mathf.Max(1, value);
      if (_level == value) {
        return;
      }

      _level = value;
      if (!suppressEvents) {
        OnLevelChanged?.Invoke(_level);
      }
    }

    public void SetExperience(int value, bool suppressEvents = false) {
      value = Mathf.Max(0, value);
      if (_experience == value) {
        return;
      }

      _experience = value;
      if (!suppressEvents) {
        OnExperienceChanged?.Invoke(_experience);
      }
    }

    public void AddExperience(int delta) {
      if (delta <= 0) {
        return;
      }

      SetExperience(_experience + delta);
    }

    public void SetCurrency(int value, bool suppressEvents = false) {
      value = Mathf.Max(0, value);
      if (_currency == value) {
        return;
      }

      _currency = value;
      if (!suppressEvents) {
        OnCurrencyChanged?.Invoke(_currency);
      }
    }

    public void AddCurrency(int delta) {
      if (delta == 0) {
        return;
      }

      int next = Mathf.Max(0, _currency + delta);
      SetCurrency(next);
    }

    public bool TrySpendCurrency(int amount) {
      if (amount <= 0) {
        return true;
      }

      if (_currency < amount) {
        return false;
      }

      SetCurrency(_currency - amount);
      return true;
    }

    public void ResetProfile(bool suppressEvents = false) {
      ApplySnapshot(new PlayerProfileData(), suppressEvents);
    }
  }
}
