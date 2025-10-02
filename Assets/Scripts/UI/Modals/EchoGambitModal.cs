using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;

namespace EchoesOfTheVoid.UI.Modals {
  public class EchoGambitModal : UIModal {
    [SerializeField] private PlayerRosterService _rosterService;

    private ListView _ruleListView;
    private TextField _ruleNameField;
    private Toggle _enabledToggle;
    private Label _conditionValueLabel;
    private Label _actionValueLabel;
    private Button _moveUpButton;
    private Button _moveDownButton;
    private Button _removeButton;
    private Button _saveButton;
    private Button _cancelButton;
    private Button _closeButton;
    private Label _errorLabel;

    private readonly List<GambitRuleViewModel> _ruleItems = new();

    private PlayerEchoData _currentEcho;
    private GambitProfileData _workingProfile;
    private int _selectedRuleIndex = -1;

    public event Action<PlayerEchoData> OnGambitApplied;

    public void ConfigureServices(PlayerRosterService rosterService) {
      _rosterService = rosterService;
    }

    public void ShowForEcho(PlayerEchoData echo) {
      if (echo == null) {
        return;
      }

      InitializeServiceIfNeeded();
      _currentEcho = echo;
      _workingProfile = RosterCloneUtility.CloneGambitProfile(echo.GambitProfile);
      RefreshRuleList();
      UpdateDetailPanel();
      if (_errorLabel != null) { _errorLabel.text = string.Empty; }
      Show();
    }

    protected override void SetupUI() {
      _ruleListView = FindElement<ListView>("rule-list");
      _ruleNameField = FindElement<TextField>("rule-name-field");
      _enabledToggle = FindElement<Toggle>("enabled-toggle");
      _conditionValueLabel = FindLabel("condition-value");
      _actionValueLabel = FindLabel("action-value");
      _moveUpButton = FindButton("move-up-button");
      _moveDownButton = FindButton("move-down-button");
      _removeButton = FindButton("remove-button");
      _saveButton = FindButton("save-button");
      _cancelButton = FindButton("cancel-button");
      _closeButton = FindButton("close-button");
      _errorLabel = FindLabel("error-label");

      ConfigureRuleList();
      UpdateActionButtons();
    }

    protected override void BindEvents() {
      if (_ruleNameField != null) {
        _ruleNameField.RegisterValueChangedCallback(evt => OnRuleNameChanged(evt.newValue));
      }

      if (_enabledToggle != null) {
        _enabledToggle.RegisterValueChangedCallback(evt => OnRuleEnabledChanged(evt.newValue));
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

    private void ConfigureRuleList() {
      if (_ruleListView == null) {
        return;
      }

      _ruleListView.itemsSource = _ruleItems;
      _ruleListView.selectionType = SelectionType.Single;
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

    private void RefreshRuleList() {
      _ruleItems.Clear();

      if (_workingProfile?.rules != null) {
        foreach (GambitRuleDefinition rule in _workingProfile.rules) {
          _ruleItems.Add(new GambitRuleViewModel(rule));
        }
      }

      _ruleListView?.RefreshItems();
      if (_ruleListView != null) {
        _ruleListView.selectedIndex = Mathf.Clamp(_selectedRuleIndex, 0, _ruleItems.Count - 1);
      }
    }

    private void OnRuleSelectionChanged(IEnumerable<object> _) {
      _selectedRuleIndex = _ruleListView?.selectedIndex ?? -1;
      UpdateDetailPanel();
      UpdateActionButtons();
    }

    private void UpdateDetailPanel() {
      if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _ruleItems.Count) {
        _ruleNameField?.SetValueWithoutNotify(string.Empty);
        _enabledToggle?.SetValueWithoutNotify(false);
        if (_conditionValueLabel != null) {
          _conditionValueLabel.text = string.Empty;
        }

        if (_actionValueLabel != null) {
          _actionValueLabel.text = string.Empty;
        }

        return;
      }

      GambitRuleDefinition rule = _workingProfile.rules[_selectedRuleIndex];
      _ruleNameField?.SetValueWithoutNotify(rule.RuleName);
      _enabledToggle?.SetValueWithoutNotify(rule.IsEnabled);
      if (_conditionValueLabel != null) {
        _conditionValueLabel.text = rule.TargetCondition != null ? rule.TargetCondition.Summary : "No condition";
      }

      if (_actionValueLabel != null) {
        _actionValueLabel.text = rule.Action != null ? rule.Action.Summary : "No action";
      }
    }

    private void OnRuleNameChanged(string newValue) {
      if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _ruleItems.Count) {
        return;
      }

      GambitRuleDefinition rule = _workingProfile.rules[_selectedRuleIndex];
      rule.RuleName = string.IsNullOrWhiteSpace(newValue) ? "New Rule" : newValue.Trim();
      RefreshRuleList();
    }

    private void OnRuleEnabledChanged(bool enabled) {
      if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _ruleItems.Count) {
        return;
      }

      GambitRuleDefinition rule = _workingProfile.rules[_selectedRuleIndex];
      rule.IsEnabled = enabled;
      RefreshRuleList();
    }

    private void MoveRule(int direction) {
      if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _ruleItems.Count) {
        return;
      }

      int newIndex = Mathf.Clamp(_selectedRuleIndex + direction, 0, _ruleItems.Count - 1);
      if (newIndex == _selectedRuleIndex) {
        return;
      }

      GambitRuleDefinition rule = _workingProfile.rules[_selectedRuleIndex];
      _workingProfile.rules.RemoveAt(_selectedRuleIndex);
      _workingProfile.rules.Insert(newIndex, rule);

      _selectedRuleIndex = newIndex;
      RefreshRuleList();
      _ruleListView.selectedIndex = newIndex;
      UpdateDetailPanel();
    }

    private void RemoveSelectedRule() {
      if (_selectedRuleIndex < 0 || _selectedRuleIndex >= _ruleItems.Count) {
        return;
      }

      _workingProfile.rules.RemoveAt(_selectedRuleIndex);
      _selectedRuleIndex = Mathf.Clamp(_selectedRuleIndex, 0, _workingProfile.rules.Count - 1);
      RefreshRuleList();
      UpdateDetailPanel();
      UpdateActionButtons();
    }

    private void SaveChanges() {
      if (_currentEcho == null || _rosterService == null) {
        return;
      }

      if (!_rosterService.TrySetGambitProfile(_currentEcho.InstanceId, _workingProfile, out string errorMessage)) {
        if (_errorLabel != null) { _errorLabel.text = errorMessage; }

        return;
      }

      if (_errorLabel != null) { _errorLabel.text = string.Empty; }
      OnGambitApplied?.Invoke(_currentEcho);
      Hide();
    }

    private void UpdateActionButtons() {
      bool hasSelection = _selectedRuleIndex >= 0 && _selectedRuleIndex < _ruleItems.Count;
      _moveUpButton?.SetEnabled(hasSelection && _selectedRuleIndex > 0);
      _moveDownButton?.SetEnabled(hasSelection && _selectedRuleIndex < _ruleItems.Count - 1);
      _removeButton?.SetEnabled(hasSelection);
      _ruleNameField?.SetEnabled(hasSelection);
      _enabledToggle?.SetEnabled(hasSelection);
      _saveButton?.SetEnabled(true);
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

