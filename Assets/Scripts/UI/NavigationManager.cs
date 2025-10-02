using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class NavigationManager : MonoBehaviour {
  public static NavigationManager Instance { get; private set; }

  [SerializeField] private UIDocument _uiDocument;

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
  }

  private void InitializeScreens() {
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

  public void RegisterScreen(string screenId, UIScreen screen) {
    if (!_screens.ContainsKey(screenId)) {
      _screens[screenId] = screen;
      screen.Initialize(_uiDocument.rootVisualElement);
    }
  }

  public void RegisterModal(string modalId, UIModal modal) {
    if (!_modals.ContainsKey(modalId)) {
      _modals[modalId] = modal;
      modal.Initialize(_uiDocument.rootVisualElement);
    }
  }

  public void NavigateToScreen(string screenId, bool addToHistory = true) {
    if (!_screens.ContainsKey(screenId)) {
      Debug.LogWarning($"Screen '{screenId}' not found!");
      return;
    }

    // Hide current screen
    if (_currentScreen != null) {

      if (addToHistory) {
        _screenHistory.Push(_currentScreen.ScreenId);
      }

      _currentScreen.Hide();
    }

    // Show new screen
    _currentScreen = _screens[screenId];
    _currentScreen.Show();

    OnScreenChanged?.Invoke(screenId);
  }

  public void NavigateBack() {
    if (_screenHistory.Count > 0) {
      string previousScreenId = _screenHistory.Pop();
      NavigateToScreen(previousScreenId, false);
    }
  }

  public void OpenModal(string modalId) {
    if (!_modals.ContainsKey(modalId)) {
      Debug.LogWarning($"Modal '{modalId}' not found!");
      return;
    }

    _modals[modalId].Show();
    OnModalOpened?.Invoke(modalId);
  }

  public void CloseModal(string modalId) {
    if (!_modals.ContainsKey(modalId)) {
      Debug.LogWarning($"Modal '{modalId}' not found!");
      return;
    }

    _modals[modalId].Hide();
    OnModalClosed?.Invoke(modalId);
  }

  public void CloseAllModals() {
    foreach (UIModal modal in _modals.Values) {
      if (modal.IsVisible) {
        modal.Hide();
      }
    }
  }

  public bool IsScreenActive(string screenId) {
    return _currentScreen != null && _currentScreen.ScreenId == screenId;
  }

  public bool IsModalOpen(string modalId) {
    return _modals.ContainsKey(modalId) && _modals[modalId].IsVisible;
  }
}


