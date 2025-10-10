using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class NavigationManager : MonoBehaviour {
  public static NavigationManager Instance { get; private set; }

  [Header("Configuration")]
  [SerializeField] private UIDocument _uiDocument;
  [SerializeField] private UIScreen _initialScreen;
  [SerializeField] private string _initialScreenId;
  [SerializeField] private bool _enableDebugLogs;

  private UIScreen _currentScreen;

  private readonly Dictionary<string, UIScreen> _screens = new();
  private readonly Dictionary<string, UIModal> _modals = new();
  private readonly Stack<string> _screenHistory = new();

  public event Action<string> OnScreenChanged;
  public event Action<string> OnModalOpened;
  public event Action<string> OnModalClosed;

  private void Awake() {
    if (Instance == null) {
      Instance = this;
      DontDestroyOnLoad(gameObject);
    } else if (Instance != this) {
      Destroy(gameObject);
      return;
    }
  }

  private void Start() {
    InitializeScreens();
    ShowInitialScreen();
  }

  private void InitializeScreens() {
    if (_uiDocument != null) {
      _uiDocument.rootVisualElement.Clear();
      LogNavigation("Cleared root visual element.");
    }

    // Find all screen components
    UIScreen[] foundScreens = FindObjectsByType<UIScreen>(FindObjectsSortMode.None);
    foreach (UIScreen screen in foundScreens) {
      RegisterScreen(screen.ScreenId, screen);
    }

    // Find all modal components
    UIModal[] foundModals = FindObjectsByType<UIModal>(FindObjectsSortMode.None);
    foreach (UIModal modal in foundModals) {
      RegisterModal(modal.ModalId, modal);
    }
  }

  private void ShowInitialScreen() {
    string targetScreenId = null;

    if (_initialScreen != null && !string.IsNullOrEmpty(_initialScreen.ScreenId)) {
      targetScreenId = _initialScreen.ScreenId;
    } else if (!string.IsNullOrEmpty(_initialScreenId)) {
      targetScreenId = _initialScreenId;
    }

    foreach (UIScreen screen in _screens.Values) {
      if (screen != null && screen.IsVisible) {
        screen.Hide();
      }
    }

    _currentScreen = null;
    _screenHistory.Clear();

    if (!string.IsNullOrEmpty(targetScreenId)) {
      NavigateToScreen(targetScreenId, false);
    } else {
      LogNavigation("No initial screen configured.");
    }
  }

  public void RegisterScreen(string screenId, UIScreen screen) {
    if (!_screens.ContainsKey(screenId)) {
      _screens[screenId] = screen;
      screen.Initialize(_uiDocument.rootVisualElement);
      LogNavigation($"Registered screen '{screenId}'.");
    } else {
      LogNavigation($"Screen '{screenId}' already registered.");
    }
  }

  public void RegisterModal(string modalId, UIModal modal) {
    if (!_modals.ContainsKey(modalId)) {
      _modals[modalId] = modal;
      modal.Initialize(_uiDocument.rootVisualElement);
      LogNavigation($"Registered modal '{modalId}'.");
    } else {
      LogNavigation($"Modal '{modalId}' already registered.");
    }
  }

  public void NavigateToScreen(string screenId, bool addToHistory = true) {
    if (!_screens.ContainsKey(screenId)) {
      Debug.LogWarning($"Screen '{screenId}' not found!");
      LogNavigation($"Failed to navigate to '{screenId}' (not registered).");
      return;
    }

    // Hide current screen
    if (_currentScreen != null) {

      if (addToHistory) {
        _screenHistory.Push(_currentScreen.ScreenId);
        LogNavigation($"Pushed '{_currentScreen.ScreenId}' onto history stack.");
      }

      _currentScreen.Hide();
      LogNavigation($"Hid screen '{_currentScreen.ScreenId}'.");
    }

    // Show new screen
    _currentScreen = _screens[screenId];
    _currentScreen.Show();
    LogNavigation($"Navigated to screen '{screenId}'.");

    OnScreenChanged?.Invoke(screenId);
  }

  public void NavigateBack() {
    if (_screenHistory.Count > 0) {
      string previousScreenId = _screenHistory.Pop();
      LogNavigation($"Popped '{previousScreenId}' from history stack.");
      NavigateToScreen(previousScreenId, false);
    } else {
      LogNavigation("NavigateBack requested but history is empty.");
    }
  }

  public void OpenModal(string modalId) {
    if (!_modals.ContainsKey(modalId)) {
      Debug.LogWarning($"Modal '{modalId}' not found!");
      LogNavigation($"Failed to open modal '{modalId}' (not registered).");
      return;
    }

    _modals[modalId].Show();
    LogNavigation($"Opened modal '{modalId}'.");
    OnModalOpened?.Invoke(modalId);
  }

  public void CloseModal(string modalId) {
    if (!_modals.ContainsKey(modalId)) {
      Debug.LogWarning($"Modal '{modalId}' not found!");
      LogNavigation($"Failed to close modal '{modalId}' (not registered).");
      return;
    }

    _modals[modalId].Hide();
    LogNavigation($"Closed modal '{modalId}'.");
    OnModalClosed?.Invoke(modalId);
  }

  public void CloseAllModals() {
    foreach (UIModal modal in _modals.Values) {
      if (modal.IsVisible) {
        modal.Hide();
        LogNavigation($"Closed modal '{modal.ModalId}' via CloseAllModals.");
      }
    }
  }

  public bool IsScreenActive(string screenId) {
    return _currentScreen != null && _currentScreen.ScreenId == screenId;
  }

  public bool IsModalOpen(string modalId) {
    return _modals.ContainsKey(modalId) && _modals[modalId].IsVisible;
  }

#if UNITY_EDITOR
  private void OnValidate() {
    if (_initialScreen != null) {
      _initialScreenId = _initialScreen.ScreenId;
    }
  }
#endif

  private void LogNavigation(string message) {
    if (!_enableDebugLogs) {
      return;
    }

    Debug.Log($"[NavigationManager] {message}", this);
  }
}
