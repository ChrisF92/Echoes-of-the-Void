using UnityEngine;
using UnityEngine.UIElements;

public class HamburgerMenu : MonoBehaviour {
  [SerializeField] private UIDocument _uiDocument;

  private Button _hamburgerButton;
  private Button _closeButton;
  private VisualElement _slidePanel;
  private VisualElement _backdrop;

  // Menu item buttons
  private Button _homeButton;
  private Button _echoesButton;
  private Button _inventoryButton;
  private Button _combatButton;
  private Button _settingsButton;

  private void Start() {
    SetupUI();
    BindEvents();
  }

  private void SetupUI() {
    VisualElement root = _uiDocument.rootVisualElement;

    // Get main elements
    _hamburgerButton = root.Q<Button>("hamburger-button");
    _closeButton = root.Q<Button>("close-button");
    _slidePanel = root.Q<VisualElement>("slide-panel");
    _backdrop = root.Q<VisualElement>("backdrop");

    // Get menu item buttons
    _homeButton = root.Q<Button>("home-button");
    _echoesButton = root.Q<Button>("echoes-button");
    _inventoryButton = root.Q<Button>("inventory-button");
    _combatButton = root.Q<Button>("combat-button");
    _settingsButton = root.Q<Button>("settings-button");

    // Ensure menu starts closed
    CloseMenu(false);
  }

  private void BindEvents() {
    // Main menu controls
    _hamburgerButton?.RegisterCallback<ClickEvent>(OnHamburgerClicked);
    _closeButton?.RegisterCallback<ClickEvent>(OnCloseClicked);
    _backdrop?.RegisterCallback<ClickEvent>(OnBackdropClicked);

    // Menu item buttons
    _homeButton?.RegisterCallback<ClickEvent>(OnHomeClicked);
    _echoesButton?.RegisterCallback<ClickEvent>(OnEchoesClicked);
    _inventoryButton?.RegisterCallback<ClickEvent>(OnInventoryClicked);
    _combatButton?.RegisterCallback<ClickEvent>(OnCombatClicked);
    _settingsButton?.RegisterCallback<ClickEvent>(OnSettingsClicked);
  }

  private void OnHamburgerClicked(ClickEvent evt) {
    if (IsOpen) {
      CloseMenu();
    } else {
      OpenMenu();
    }
  }

  private void OnCloseClicked(ClickEvent evt) {
    CloseMenu();
  }

  private void OnBackdropClicked(ClickEvent evt) {
    CloseMenu();
  }

  public void OpenMenu(bool animate = true) {
    if (IsOpen) {
      return;
    }

    IsOpen = true;
    _slidePanel?.RemoveFromClassList("slide-panel--hidden");
  }

  public void CloseMenu(bool animate = true) {
    if (!IsOpen && animate) {
      return;
    }

    IsOpen = false;
    _slidePanel?.AddToClassList("slide-panel--hidden");
  }

  // Menu item click handlers
  private void OnHomeClicked(ClickEvent evt) {
    NavigationManager.Instance.NavigateToScreen("HomeScreen");
    CloseMenu();
  }

  private void OnEchoesClicked(ClickEvent evt) {
    NavigationManager.Instance.NavigateToScreen("EchoesScreen");
    CloseMenu();
  }

  private void OnInventoryClicked(ClickEvent evt) {
    NavigationManager.Instance.NavigateToScreen("InventoryScreen");
    CloseMenu();
  }

  private void OnCombatClicked(ClickEvent evt) {
    NavigationManager.Instance.NavigateToScreen("CombatScreen");
    CloseMenu();
  }

  private void OnSettingsClicked(ClickEvent evt) {
    NavigationManager.Instance.OpenModal("SettingsModal");
    CloseMenu();
  }

  // Public methods for external control
  public bool IsOpen { get; private set; } = false;

  public void SetMenuItemActive(string menuItem, bool active) {
    Button button = menuItem.ToLower() switch {
      "home" => _homeButton,
      "echoes" => _echoesButton,
      "inventory" => _inventoryButton,
      "combat" => _combatButton,
      "settings" => _settingsButton,
      _ => null
    };

    if (button != null) {
      if (active) {
        button.AddToClassList("menu-item-button--active");
      } else {
        button.RemoveFromClassList("menu-item-button--active");
      }
    }
  }
}