using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Gambits.Blocks.Implementations;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory;
using EchoesOfTheVoid.Core.Inventory.Player;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using EchoesOfTheVoid.Core.Inventory.Systems;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;

namespace EchoesOfTheVoid.UI.Modals {
  public class EchoGambitModal : UIModal {
    [SerializeField] private PlayerRosterService _rosterService;
    [SerializeField] private PlayerInventory _playerInventory;

    private ListView _ruleListView;
    private TextField _ruleNameField;
    private Toggle _enabledToggle;
    private Button _conditionButton;
    private Button _actionButton;
    private Button _moveUpButton;
    private Button _moveDownButton;
    private Button _removeButton;
    private Button _saveButton;
    private Button _cancelButton;
    private Button _closeButton;
    private Button _addRuleButton;
    private Button[] _slotButtons = Array.Empty<Button>();
    private TextField _slotNameField;
    private Label _activeSlotLabel;
    private Button _setActiveSlotButton;
    private Label _errorLabel;

    private readonly List<GambitRuleViewModel> _ruleItems = new();
    private readonly List<GambitProfileData> _workingProfiles = new();

    private PlayerEchoData _currentEcho;
    private GambitProfileData _workingProfile;
    private int _selectedRuleIndex = -1;
    private int _selectedSlotIndex = -1;
    private int _pendingActiveSlotIndex;

    public event Action<PlayerEchoData> OnGambitApplied;

    public void ConfigureServices(PlayerRosterService rosterService, PlayerInventory playerInventory = null) {
      _rosterService = rosterService;
      _playerInventory = playerInventory;
    }

    public void ShowForEcho(PlayerEchoData echo) {
      if (echo == null) {
        return;
      }

      InitializeServiceIfNeeded();
      InitializeInventoryIfNeeded();

      _currentEcho = echo;
      LoadWorkingProfiles(echo);
      SelectSlot(_pendingActiveSlotIndex);
      UpdateDetailPanel();
      UpdateActionButtons();
      UpdateSlotUI();

      if (_errorLabel != null) {
        _errorLabel.text = string.Empty;
      }

      Show();
    }

    protected override void SetupUI() {
      _ruleListView = FindElement<ListView>("rule-list");
      _ruleNameField = FindElement<TextField>("rule-name-field");
      _enabledToggle = FindElement<Toggle>("enabled-toggle");
      _conditionButton = FindButton("condition-value");
      _actionButton = FindButton("action-value");
      _moveUpButton = FindButton("move-up-button");
      _moveDownButton = FindButton("move-down-button");
      _removeButton = FindButton("remove-button");
      _saveButton = FindButton("save-button");
      _cancelButton = FindButton("cancel-button");
      _closeButton = FindButton("close-button");
      _addRuleButton = FindButton("add-rule-button");
      _slotButtons = new[] {
        FindButton("slot-button-0"),
        FindButton("slot-button-1"),
        FindButton("slot-button-2")
      };
      _slotNameField = FindElement<TextField>("slot-name-field");
      _activeSlotLabel = FindLabel("active-slot-label");
      _setActiveSlotButton = FindButton("set-active-button");
      _errorLabel = FindLabel("error-label");

      ConfigureRuleList();
      UpdateActionButtons();
      UpdateSlotUI();
    }

    protected override void BindEvents() {
      if (_ruleNameField != null) {
        _ruleNameField.RegisterValueChangedCallback(evt => OnRuleNameChanged(evt.newValue));
      }

      if (_enabledToggle != null) {
        _enabledToggle.RegisterValueChangedCallback(evt => OnRuleEnabledChanged(evt.newValue));
      }

      if (_addRuleButton != null) {
        _addRuleButton.RegisterCallback<ClickEvent>(_ => AddRule());
      }

      if (_conditionButton != null) {
        _conditionButton.RegisterCallback<ClickEvent>(_ => ShowConditionMenu());
      }

      if (_actionButton != null) {
        _actionButton.RegisterCallback<ClickEvent>(_ => ShowActionMenu());
      }

      if (_slotNameField != null) {
        _slotNameField.RegisterValueChangedCallback(evt => OnSlotNameChanged(evt.newValue));
      }

      if (_setActiveSlotButton != null) {
        _setActiveSlotButton.RegisterCallback<ClickEvent>(_ => SetActiveSlotFromSelection());
      }

      for (int i = 0; i < _slotButtons.Length; i++) {
        if (_slotButtons[i] == null) {
          continue;
        }

        int capturedIndex = i;
        _slotButtons[i].RegisterCallback<ClickEvent>(_ => SelectSlot(capturedIndex));
      }

      _moveUpButton?.RegisterCallback<ClickEvent>(_ => MoveRule(-1));
      _moveDownButton?.RegisterCallback<ClickEvent>(_ => MoveRule(1));
      _removeButton?.RegisterCallback<ClickEvent>(_ => RemoveSelectedRule());
      _saveButton?.RegisterCallback<ClickEvent>(_ => SaveChanges());
      _cancelButton?.RegisterCallback<ClickEvent>(_ => Hide());
      _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());
    }

    private void InitializeServiceIfNeeded() {
      if (_rosterService == null) {
        _rosterService = FindFirstObjectByType<PlayerRosterService>();
      }
    }

    private void InitializeInventoryIfNeeded() {
      if (_playerInventory != null) {
        return;
      }

      if (_rosterService != null) {
        _playerInventory = _rosterService.GetComponent<PlayerInventory>() ?? _rosterService.GetComponentInParent<PlayerInventory>();
      }

      if (_playerInventory == null) {
        _playerInventory = FindFirstObjectByType<PlayerInventory>();
      }
    }

    private void LoadWorkingProfiles(PlayerEchoData echo) {
      _workingProfiles.Clear();

      if (echo?.GambitProfiles != null) {
        foreach (GambitProfileData profile in echo.GambitProfiles) {
          _workingProfiles.Add(RosterCloneUtility.CloneGambitProfile(profile));
        }
      }

      while (_workingProfiles.Count < PlayerEchoData.GambitProfileSlotCount) {
        _workingProfiles.Add(new GambitProfileData());
      }

      if (_workingProfiles.Count > PlayerEchoData.GambitProfileSlotCount) {
        _workingProfiles.RemoveRange(PlayerEchoData.GambitProfileSlotCount, _workingProfiles.Count - PlayerEchoData.GambitProfileSlotCount);
      }

      _pendingActiveSlotIndex = Mathf.Clamp(echo?.ActiveGambitSlot ?? 0, 0, _workingProfiles.Count - 1);
    }

    private void ConfigureRuleList() {
      if (_ruleListView == null) {
        return;
      }

      _ruleListView.itemsSource = _ruleItems;
      _ruleListView.selectionType = SelectionType.Single;
      _ruleListView.fixedItemHeight = 56f;
      _ruleListView.makeItem = () => {
        var root = new VisualElement();
        root.AddToClassList("gambit-rule-item");

        var nameLabel = new Label { name = "gambit-rule-item__name" };
        nameLabel.AddToClassList("gambit-rule-item__name");
        root.Add(nameLabel);

        var summaryLabel = new Label { name = "gambit-rule-item__summary" };
        summaryLabel.AddToClassList("gambit-rule-item__summary");
        root.Add(summaryLabel);

        return root;
      };

      _ruleListView.bindItem = (element, index) => {
        if (index < 0 || index >= _ruleItems.Count) {
          return;
        }

        GambitRuleViewModel viewModel = _ruleItems[index];
        Label nameLabel = element.Q<Label>("gambit-rule-item__name");
        if (nameLabel != null) {
          nameLabel.text = viewModel.RuleName;
        }

        Label summaryLabel = element.Q<Label>("gambit-rule-item__summary");
        if (summaryLabel != null) {
          summaryLabel.text = viewModel.Summary;
        }
      };

      _ruleListView.selectionChanged += OnRuleSelectionChanged;
    }

    private void SelectSlot(int slotIndex) {
      if (_workingProfiles.Count == 0) {
        _selectedSlotIndex = -1;
        _workingProfile = null;
        _selectedRuleIndex = -1;
        RefreshRuleList();
        UpdateDetailPanel();
        UpdateActionButtons();
        UpdateSlotUI();
        return;
      }

      int clamped = Mathf.Clamp(slotIndex, 0, _workingProfiles.Count - 1);
      _selectedSlotIndex = clamped;
      _workingProfile = _workingProfiles[clamped];
      _selectedRuleIndex = -1;

      RefreshRuleList();
      UpdateDetailPanel();
      UpdateActionButtons();
      UpdateSlotUI();
    }

    private void RefreshRuleList() {
      _ruleItems.Clear();

      if (_workingProfile?.rules != null) {
        foreach (GambitRuleDefinition rule in _workingProfile.rules) {
          _ruleItems.Add(new GambitRuleViewModel(rule));
        }
      }

      _ruleListView?.RefreshItems();

      if (_ruleListView == null) {
        return;
      }

      if (_ruleItems.Count == 0) {
        _selectedRuleIndex = -1;
        _ruleListView.selectedIndex = -1;
      } else if (_selectedRuleIndex >= 0) {
        _selectedRuleIndex = Mathf.Clamp(_selectedRuleIndex, 0, _ruleItems.Count - 1);
        _ruleListView.selectedIndex = _selectedRuleIndex;
      } else {
        _ruleListView.selectedIndex = -1;
      }
    }

    private void OnRuleSelectionChanged(IEnumerable<object> _) {
      _selectedRuleIndex = _ruleListView?.selectedIndex ?? -1;
      UpdateDetailPanel();
      UpdateActionButtons();
    }

    private void UpdateDetailPanel() {
      if (!TryGetSelectedRule(out GambitRuleDefinition rule)) {
        _ruleNameField?.SetValueWithoutNotify(string.Empty);
        _enabledToggle?.SetValueWithoutNotify(false);
        if (_conditionButton != null) {
          _conditionButton.text = "Select condition";
        }

        if (_actionButton != null) {
          _actionButton.text = "Select action";
        }

        return;
      }

      _ruleNameField?.SetValueWithoutNotify(rule.RuleName);
      _enabledToggle?.SetValueWithoutNotify(rule.IsEnabled);
      if (_conditionButton != null) {
        _conditionButton.text = rule.TargetCondition != null ? rule.TargetCondition.Summary : "No condition";
      }

      if (_actionButton != null) {
        _actionButton.text = rule.Action != null ? rule.Action.Summary : "No action";
      }
    }

    private void OnRuleNameChanged(string newValue) {
      if (!TryGetSelectedRule(out GambitRuleDefinition rule)) {
        return;
      }

      rule.RuleName = string.IsNullOrWhiteSpace(newValue) ? "New Rule" : newValue.Trim();
      RefreshRuleList();
    }

    private void OnRuleEnabledChanged(bool enabled) {
      if (!TryGetSelectedRule(out GambitRuleDefinition rule)) {
        return;
      }

      rule.IsEnabled = enabled;
      RefreshRuleList();
    }

    private void AddRule() {
      if (_workingProfile == null) {
        return;
      }

      _workingProfile.rules ??= new List<GambitRuleDefinition>();
      int nextIndex = _workingProfile.rules.Count + 1;
      var newRule = new GambitRuleDefinition {
        RuleName = $"Rule {nextIndex}",
        IsEnabled = true,
        TargetCondition = new RandomEnemyTargetBlock(),
        Action = new AttackActionBlock()
      };

      _workingProfile.rules.Add(newRule);
      _selectedRuleIndex = _workingProfile.rules.Count - 1;
      RefreshRuleList();
      UpdateDetailPanel();
      UpdateActionButtons();
    }

    private void MoveRule(int direction) {
      if (!TryGetSelectedRule(out GambitRuleDefinition rule) || _workingProfile?.rules == null) {
        return;
      }

      int newIndex = Mathf.Clamp(_selectedRuleIndex + direction, 0, _workingProfile.rules.Count - 1);
      if (newIndex == _selectedRuleIndex) {
        return;
      }

      _workingProfile.rules.RemoveAt(_selectedRuleIndex);
      _workingProfile.rules.Insert(newIndex, rule);

      _selectedRuleIndex = newIndex;
      RefreshRuleList();
      UpdateDetailPanel();
      UpdateActionButtons();
    }

    private void RemoveSelectedRule() {
      if (_workingProfile?.rules == null || !TryGetSelectedRule(out _)) {
        return;
      }

      _workingProfile.rules.RemoveAt(_selectedRuleIndex);
      if (_workingProfile.rules.Count == 0) {
        _selectedRuleIndex = -1;
      } else {
        _selectedRuleIndex = Mathf.Clamp(_selectedRuleIndex, 0, _workingProfile.rules.Count - 1);
      }

      RefreshRuleList();
      UpdateDetailPanel();
      UpdateActionButtons();
    }

    private void SaveChanges() {
      if (_currentEcho == null || _rosterService == null) {
        return;
      }

      for (int i = 0; i < _workingProfiles.Count; i++) {
        GambitProfileData profile = _workingProfiles[i] ?? new GambitProfileData();
        bool setActive = i == _pendingActiveSlotIndex;
        if (!_rosterService.TrySetGambitProfile(_currentEcho.InstanceId, profile, out string errorMessage, i, setActive)) {
          if (_errorLabel != null) {
            _errorLabel.text = errorMessage;
          }

          return;
        }
      }

      if (_errorLabel != null) {
        _errorLabel.text = string.Empty;
      }

      OnGambitApplied?.Invoke(_currentEcho);
      Hide();
    }

    private void UpdateActionButtons() {
      bool hasSelection = TryGetSelectedRule(out _);

      _moveUpButton?.SetEnabled(hasSelection && _selectedRuleIndex > 0);
      _moveDownButton?.SetEnabled(hasSelection && _workingProfile != null && _workingProfile.rules != null && _selectedRuleIndex < _workingProfile.rules.Count - 1);
      _removeButton?.SetEnabled(hasSelection);
      _ruleNameField?.SetEnabled(hasSelection);
      _enabledToggle?.SetEnabled(hasSelection);
      _conditionButton?.SetEnabled(hasSelection);
      _actionButton?.SetEnabled(hasSelection);
      _addRuleButton?.SetEnabled(_workingProfile != null);

      if (_slotNameField != null) {
        _slotNameField.SetEnabled(_selectedSlotIndex >= 0);
      }

      if (_setActiveSlotButton != null) {
        _setActiveSlotButton.SetEnabled(_selectedSlotIndex >= 0 && _selectedSlotIndex != _pendingActiveSlotIndex);
      }

      _saveButton?.SetEnabled(true);
    }

    private void UpdateSlotUI() {
      for (int i = 0; i < _slotButtons.Length; i++) {
        Button button = _slotButtons[i];
        if (button == null) {
          continue;
        }

        GambitProfileData profile = i < _workingProfiles.Count ? _workingProfiles[i] : null;
        string displayName = !string.IsNullOrWhiteSpace(profile?.DisplayName) ? profile.DisplayName : "Slot";
        button.text = $"{i + 1}. {displayName}";
        button.EnableInClassList("gambit-slot-selector__button--selected", i == _selectedSlotIndex);
        button.EnableInClassList("gambit-slot-selector__button--active", i == _pendingActiveSlotIndex);
      }

      if (_activeSlotLabel != null) {
        _activeSlotLabel.text = $"Active Slot: {_pendingActiveSlotIndex + 1}";
      }

      if (_slotNameField != null) {
        if (_selectedSlotIndex >= 0 && _selectedSlotIndex < _workingProfiles.Count) {
          GambitProfileData profile = _workingProfiles[_selectedSlotIndex];
          _slotNameField.SetValueWithoutNotify(profile?.displayName ?? string.Empty);
        } else {
          _slotNameField.SetValueWithoutNotify(string.Empty);
        }
      }
    }

    private void OnSlotNameChanged(string newValue) {
      if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _workingProfiles.Count) {
        return;
      }

      GambitProfileData profile = _workingProfiles[_selectedSlotIndex];
      if (profile == null) {
        profile = new GambitProfileData();
        _workingProfiles[_selectedSlotIndex] = profile;
      }

      profile.displayName = string.IsNullOrWhiteSpace(newValue) ? string.Empty : newValue.Trim();
      UpdateSlotUI();
    }

    private void SetActiveSlotFromSelection() {
      if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _workingProfiles.Count) {
        return;
      }

      _pendingActiveSlotIndex = _selectedSlotIndex;
      UpdateSlotUI();
      UpdateActionButtons();
    }

    private void ShowConditionMenu() {
      if (_conditionButton == null || !TryGetSelectedRule(out _)) {
        return;
      }

      var menu = new GenericDropdownMenu();
      menu.AddItem("Clear", false, () => SetRuleCondition(null));
      menu.AddSeparator(string.Empty);
      menu.AddItem("Enemies/Random Enemy", false, () => SetRuleCondition(new RandomEnemyTargetBlock()));
      menu.AddItem("Self/Self", false, () => SetRuleCondition(new SelfTargetBlock()));
      menu.AddSeparator("Allies/");
      menu.AddItem("Allies/HP < 75% (Incl. Self)", false, () => SetRuleCondition(new AllyHealthBelowPercentBlock {
        Threshold = 0.75f,
        IncludeSelf = true
      }));
      menu.AddItem("Allies/HP < 50% (Incl. Self)", false, () => SetRuleCondition(new AllyHealthBelowPercentBlock {
        Threshold = 0.5f,
        IncludeSelf = true
      }));
      menu.AddItem("Allies/HP < 35% (Incl. Self)", false, () => SetRuleCondition(new AllyHealthBelowPercentBlock {
        Threshold = 0.35f,
        IncludeSelf = true
      }));
      menu.AddItem("Allies/HP < 35% (Exclude Self)", false, () => SetRuleCondition(new AllyHealthBelowPercentBlock {
        Threshold = 0.35f,
        IncludeSelf = false
      }));

      menu.DropDown(_conditionButton.worldBound, _conditionButton, true);
    }

    private void ShowActionMenu() {
      if (_actionButton == null || !TryGetSelectedRule(out _)) {
        return;
      }

      var menu = new GenericDropdownMenu();
      menu.AddItem("Clear", false, () => SetRuleAction(null));
      menu.AddSeparator(string.Empty);
      menu.AddItem("Basic/Attack", false, () => SetRuleAction(new AttackActionBlock()));
      menu.AddItem("Basic/Defend", false, () => SetRuleAction(new DefendActionBlock()));

      var skills = EnumerateAvailableSkills().ToList();
      if (skills.Count > 0) {
        foreach (SkillSO skill in skills) {
          SkillSO capturedSkill = skill;
          menu.AddItem($"Skills/{capturedSkill.DisplayName}", false, () => SetRuleAction(new SkillActionBlock {
            skill = capturedSkill,
            requireCanUse = true
          }));
        }
      } else {
        menu.AddDisabledItem("Skills/No available skills", false);
      }

      var items = EnumerateAvailableItems().ToList();
      if (items.Count > 0) {
        foreach (ItemScriptableObject item in items) {
          ItemScriptableObject capturedItem = item;
          menu.AddItem($"Items/{capturedItem.DisplayName}", false, () => SetRuleAction(new ItemActionBlock {
            item = capturedItem,
            requireAvailability = true
          }));
        }
      } else {
        menu.AddDisabledItem("Items/No available items", false);
      }

      menu.DropDown(_actionButton.worldBound, _actionButton, true);
    }

    private void SetRuleCondition(TargetConditionBlock condition) {
      if (!TryGetSelectedRule(out GambitRuleDefinition rule)) {
        return;
      }

      rule.TargetCondition = condition;
      RefreshRuleList();
      UpdateDetailPanel();
    }

    private void SetRuleAction(GambitActionBlock action) {
      if (!TryGetSelectedRule(out GambitRuleDefinition rule)) {
        return;
      }

      rule.Action = action;
      RefreshRuleList();
      UpdateDetailPanel();
    }

    private bool TryGetSelectedRule(out GambitRuleDefinition rule) {
      rule = null;
      if (_workingProfile?.rules == null) {
        return false;
      }

      if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _workingProfile.rules.Count) {
        return false;
      }

      rule = _workingProfile.rules[_selectedRuleIndex];
      return rule != null;
    }

    private IEnumerable<SkillSO> EnumerateAvailableSkills() {
      if (_currentEcho?.Template?.StartingSkills == null) {
        yield break;
      }

      var seen = new HashSet<SkillSO>();
      foreach (SkillSO skill in _currentEcho.Template.StartingSkills) {
        if (skill == null || !seen.Add(skill)) {
          continue;
        }

        yield return skill;
      }
    }

    private IEnumerable<ItemScriptableObject> EnumerateAvailableItems() {
      InitializeInventoryIfNeeded();
      if (_playerInventory?.Slots == null) {
        yield break;
      }

      var seen = new HashSet<ItemScriptableObject>();
      foreach (InventorySlot slot in _playerInventory.Slots) {
        if (slot == null || slot.IsEmpty) {
          continue;
        }

        ItemScriptableObject item = slot.Item;
        if (item == null || item.ItemType != ItemType.Consumable || !item.ConsumableInCombat) {
          continue;
        }

        if (!seen.Add(item)) {
          continue;
        }

        yield return item;
      }
    }

    private void ConfigureTextFieldAppearance(TextField field) {
      if (field == null) {
        return;
      }

      var textColor = new Color32(11, 19, 36, 255);
      var backgroundColor = new Color32(240, 244, 255, 235);

      field.style.color = new StyleColor(textColor);
      field.style.backgroundColor = new StyleColor(backgroundColor);

      VisualElement input = field.Q(TextInputBaseField<string>.textInputUssName);
      if (input != null) {
        input.style.color = new StyleColor(textColor);
        input.style.backgroundColor = new StyleColor(backgroundColor);
      }
    }

    private struct GambitRuleViewModel {
      public GambitRuleViewModel(GambitRuleDefinition rule) {
        Rule = rule;
        RuleName = rule != null ? rule.RuleName : string.Empty;
        Summary = rule != null
          ? $"{(rule.IsEnabled ? "[On]" : "[Off]")} {rule.TargetCondition?.Summary ?? "No Condition"} -> {rule.Action?.Summary ?? "No Action"}"
          : string.Empty;
      }

      public GambitRuleDefinition Rule { get; }
      public string RuleName { get; }
      public string Summary { get; }
    }
  }
}

