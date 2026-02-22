using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Roster;
using EchoesOfTheVoid.Core.Roster.Data;
using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using EchoesOfTheVoid.Core.Roster.Progression.Definitions;
using EchoesOfTheVoid.Core.Roster.Progression.Results;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EchoesOfTheVoid.UI.Modals {
  public class EchoSkillNodeDetailModal : UIModal {
    [SerializeField] private PlayerRosterService _rosterService;

    private Label _titleLabel;
    private Label _stateLabel;
    private Label _descriptionLabel;
    private Label _costLabel;
    private Label _availableLabel;
    private VisualElement _prerequisiteList;
    private Label _feedbackLabel;
    private Button _unlockButton;
    private Button _closeButton;

    private PlayerEchoData _currentEcho;
    private IEchoSkillTreeDefinition _currentSkillTree;
    private EchoSkillTreeModal.NodeViewModel _currentViewModel;
    private bool _hasCurrentNode;

    public event Action<string> OnNodeUnlockSucceeded;

    public string CurrentNodeId {
      get {
        return _hasCurrentNode && _currentViewModel.Definition != null
          ? _currentViewModel.Definition.NodeId
          : string.Empty;
      }
    }

#if UNITY_EDITOR
    private void OnValidate() {
      if (string.IsNullOrEmpty(_modalId)) {
        _modalId = "EchoSkillNodeDetailModal";
      }

      if (_modalTemplate == null) {
        _modalTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Modals/EchoSkillNodeDetailModal.uxml");
      }
    }
#endif

    public void ConfigureServices(PlayerRosterService rosterService) {
      _rosterService = rosterService;
    }

    public void PresentNode(PlayerEchoData echo, IEchoSkillTreeDefinition skillTree, EchoSkillTreeModal.NodeViewModel viewModel) {
      if (echo == null || skillTree == null || viewModel.Definition == null) {
        return;
      }

      ResetFeedback();
      _currentEcho = echo;
      _currentSkillTree = skillTree;
      _currentViewModel = viewModel;
      _hasCurrentNode = true;

      UpdateContent();
      if (!IsVisible) {
        Show();
      }
    }

    public void RefreshWithViewModel(PlayerEchoData echo, EchoSkillTreeModal.NodeViewModel viewModel) {
      if (!_hasCurrentNode) {
        return;
      }

      _currentEcho = echo ?? _currentEcho;
      _currentViewModel = viewModel.Definition != null ? viewModel : _currentViewModel;
      UpdateContent();
    }

    protected override void SetupUI() {
      _titleLabel = FindLabel("node-title");
      _stateLabel = FindLabel("node-state");
      _descriptionLabel = FindLabel("node-description");
      _costLabel = FindLabel("node-cost");
      _availableLabel = FindLabel("node-available");
      _prerequisiteList = FindElement<VisualElement>("node-prerequisites");
      _feedbackLabel = FindLabel("node-feedback");
      _unlockButton = FindButton("unlock-button");
      _closeButton = FindButton("close-button");

      ResetFeedback();
    }

    protected override void BindEvents() {
      base.BindEvents();

      if (_unlockButton != null) {
        _unlockButton.clicked += OnUnlockClicked;
      }

      if (_closeButton != null) {
        _closeButton.clicked += Hide;
      }
    }

    protected override void OnHide() {
      base.OnHide();
      ResetFeedback();
    }

    private void OnUnlockClicked() {
      if (!_hasCurrentNode || _currentEcho == null || _rosterService == null) {
        ShowFeedback("Unable to unlock right now.");
        return;
      }

      string nodeId = _currentViewModel.Definition.NodeId;
      if (string.IsNullOrEmpty(nodeId)) {
        ShowFeedback("Invalid skill node.");
        return;
      }

      bool success = _rosterService.TryUnlockSkillNode(_currentEcho.InstanceId, nodeId, out SkillUnlockResult result, out string errorMessage);
      if (!success) {
        ShowFeedback(string.IsNullOrWhiteSpace(errorMessage) ? "Failed to unlock skill node." : errorMessage);
        UpdateContent();
        return;
      }

      ShowFeedback($"Unlocked '{_currentViewModel.Definition.DisplayName}'.");
      OnNodeUnlockSucceeded?.Invoke(nodeId);
    }

    private void UpdateContent() {
      if (!_hasCurrentNode) {
        return;
      }

      SkillTreeNodeDefinition definition = _currentViewModel.Definition;

      if (_titleLabel != null) {
        _titleLabel.text = definition.DisplayName;
      }

      if (_stateLabel != null) {
        _stateLabel.text = ResolveStateLabel(_currentViewModel);
        UpdateStateClass();
      }

      if (_descriptionLabel != null) {
        string description = string.IsNullOrWhiteSpace(definition.Description)
          ? "No description provided."
          : definition.Description;
        _descriptionLabel.text = description;
      }

      if (_costLabel != null) {
        _costLabel.text = $"{definition.SkillPointCost} SP";
      }

      if (_availableLabel != null) {
        int available = _currentEcho != null ? _currentEcho.UnspentSkillPoints : 0;
        _availableLabel.text = $"{available} SP";
      }

      PopulatePrerequisites(definition);
      UpdateActionState();
    }

    private void PopulatePrerequisites(SkillTreeNodeDefinition definition) {
      if (_prerequisiteList == null) {
        return;
      }

      _prerequisiteList.Clear();

      IReadOnlyList<string> prerequisiteIds = definition.PrerequisiteNodeIds;
      if (prerequisiteIds == null || prerequisiteIds.Count == 0) {
        var noneLabel = new Label("No prerequisites");
        noneLabel.AddToClassList("skill-node-modal__prerequisite");
        noneLabel.AddToClassList("skill-node-modal__prerequisite--met");
        _prerequisiteList.Add(noneLabel);
        return;
      }

      for (int i = 0; i < prerequisiteIds.Count; i++) {
        string prerequisiteId = prerequisiteIds[i];
        if (string.IsNullOrWhiteSpace(prerequisiteId)) {
          continue;
        }

        SkillTreeNodeDefinition requiredNode = null;
        _currentSkillTree?.TryGetNode(prerequisiteId, out requiredNode);
        string displayName = requiredNode != null
          ? requiredNode.DisplayName
          : prerequisiteId;

        bool isUnlocked = _currentEcho != null && _currentEcho.HasUnlockedSkillNode(prerequisiteId);

        var row = new VisualElement();
        row.AddToClassList("skill-node-modal__prerequisite");
        row.AddToClassList(isUnlocked
          ? "skill-node-modal__prerequisite--met"
          : "skill-node-modal__prerequisite--locked");

        var bullet = new VisualElement();
        bullet.AddToClassList("skill-node-modal__prerequisite-indicator");
        row.Add(bullet);

        var label = new Label(displayName);
        label.AddToClassList("skill-node-modal__prerequisite-label");
        row.Add(label);

        _prerequisiteList.Add(row);
      }
    }

    private void UpdateActionState() {
      if (_unlockButton == null) {
        return;
      }

      bool canUnlock = _currentViewModel.State == EchoSkillTreeModal.NodeState.Unlockable
        && _currentViewModel.HasEnoughSkillPoints;

      _unlockButton.text = _currentViewModel.State == EchoSkillTreeModal.NodeState.Unlocked ? "Unlocked" : "Unlock";
      _unlockButton.SetEnabled(canUnlock);
      _unlockButton.style.display = _currentViewModel.State == EchoSkillTreeModal.NodeState.Unlocked ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void UpdateStateClass() {
      if (_stateLabel == null) {
        return;
      }

      _stateLabel.RemoveFromClassList("skill-node-modal__state--locked");
      _stateLabel.RemoveFromClassList("skill-node-modal__state--unlockable");
      _stateLabel.RemoveFromClassList("skill-node-modal__state--unlocked");

      switch (_currentViewModel.State) {
        case EchoSkillTreeModal.NodeState.Unlockable:
          _stateLabel.AddToClassList("skill-node-modal__state--unlockable");
          break;
        case EchoSkillTreeModal.NodeState.Unlocked:
          _stateLabel.AddToClassList("skill-node-modal__state--unlocked");
          break;
        default:
          _stateLabel.AddToClassList("skill-node-modal__state--locked");
          break;
      }
    }

    private static string ResolveStateLabel(EchoSkillTreeModal.NodeViewModel viewModel) {
      return viewModel.State switch {
        EchoSkillTreeModal.NodeState.Unlocked => "Unlocked",
        EchoSkillTreeModal.NodeState.Unlockable when viewModel.HasEnoughSkillPoints => "Unlockable",
        EchoSkillTreeModal.NodeState.Unlockable => "Need Skill Points",
        _ => viewModel.PrerequisitesMet ? "Locked" : "Prerequisites Locked"
      };
    }

    private void ShowFeedback(string message) {
      if (_feedbackLabel != null) {
        _feedbackLabel.text = message ?? string.Empty;
      }
    }

    private void ResetFeedback() {
      ShowFeedback(string.Empty);
    }
  }
}
