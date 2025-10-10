using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using ItemData = EchoesOfTheVoid.Core.Inventory.ScriptableObjects.ItemScriptableObject;

namespace EchoesOfTheVoid.UI.Combat {
  /// <summary>
  /// Handles combat action controls, including action buttons and selection modals.
  /// </summary>
  public sealed class CombatActionController {
    private readonly VisualElement _root;
    private readonly List<ItemData> _itemSource = new();
    private readonly List<SkillSO> _skillSource = new();
    private readonly Dictionary<string, SkillOptionState> _skillStates = new();

    private Button _attackButton;
    private Button _defendButton;
    private Button _itemButton;
    private Button _skillButton;
    private Button _autoAllButton;
    private Button _itemCloseButton;
    private Button _skillCloseButton;

    private VisualElement _itemModal;
    private VisualElement _skillModal;
    private ListView _itemList;
    private Label _itemEmptyMessage;
    private ListView _skillList;

    private const string EnemyTurnClass = "is-enemy-turn";
    private const string DefaultItemEmptyMessage = "No usable items available.";

    public CombatActionController(VisualElement root) {
      _root = root;
    }

    public readonly struct SkillOptionState {
      public SkillOptionState(bool isAvailable, int remainingCooldownTurns, string unavailableReason = null) {
        IsAvailable = isAvailable;
        RemainingCooldownTurns = Mathf.Max(0, remainingCooldownTurns);
        UnavailableReason = string.IsNullOrWhiteSpace(unavailableReason) ? string.Empty : unavailableReason.Trim();
      }

      public bool IsAvailable { get; }
      public int RemainingCooldownTurns { get; }
      public string UnavailableReason { get; }
    }

    public event Action<CombatActionType> ActionRequested;
    public event Action<ItemData> ItemSelected;
    public event Action<SkillSO> SkillSelected;
    public event Action ModalsClosed;
    public event Action AutoAllRequested;

    public void Initialize() {
      _attackButton = _root.Q<Button>("attack-btn");
      _defendButton = _root.Q<Button>("defend-btn");
      _itemButton = _root.Q<Button>("item-btn");
      _skillButton = _root.Q<Button>("skill-btn");
      _autoAllButton = _root.Q<Button>("auto-all-btn");
      _itemCloseButton = _root.Q<Button>("item-close-btn");
      _skillCloseButton = _root.Q<Button>("skill-close-btn");

      _itemModal = _root.Q<VisualElement>("item-modal");
      _skillModal = _root.Q<VisualElement>("skill-modal");
      _itemList = _root.Q<ListView>("item-list");
      _skillList = _root.Q<ListView>("skill-list");
      _itemEmptyMessage = _root.Q<Label>("item-empty-message");

      ConfigureListView(_itemList, BindItemEntry, HandleItemSelectionChanged);
      ConfigureListView(_skillList, BindSkillEntry, HandleSkillSelectionChanged);

      _attackButton?.RegisterCallback<ClickEvent>(_ => ActionRequested?.Invoke(CombatActionType.Attack));
      _defendButton?.RegisterCallback<ClickEvent>(_ => ActionRequested?.Invoke(CombatActionType.Defend));
      _itemButton?.RegisterCallback<ClickEvent>(_ => ActionRequested?.Invoke(CombatActionType.Item));
      _skillButton?.RegisterCallback<ClickEvent>(_ => ActionRequested?.Invoke(CombatActionType.Skill));
      _autoAllButton?.RegisterCallback<ClickEvent>(_ => AutoAllRequested?.Invoke());

      _itemCloseButton?.RegisterCallback<ClickEvent>(_ => HideModals());
      _skillCloseButton?.RegisterCallback<ClickEvent>(_ => HideModals());

      HideModals(false);
    }

    public void RefreshActionAvailability(Combatant activeCombatant) {
      bool isPlayerTurn = activeCombatant != null &&
                          activeCombatant.IsAlive &&
                          activeCombatant.Team == CombatTeam.Player;
      bool isEnemyTurn = !isPlayerTurn;

      bool hideForAuto = isPlayerTurn && activeCombatant?.IsAutoCombatEnabled == true;
      bool hideForEnemyTurn = !isPlayerTurn;

      bool shouldHideButtons = hideForAuto || hideForEnemyTurn;
      SetActionButtonsVisibility(!shouldHideButtons);
      if (shouldHideButtons) {
        HideModals(false);
      }

      SetButtonState(_attackButton, isPlayerTurn, isEnemyTurn);
      SetButtonState(_defendButton, isPlayerTurn, isEnemyTurn);

      bool hasInventory = isPlayerTurn && activeCombatant?.GetComponent<InventoryComponent>() != null;
      SetButtonState(_itemButton, hasInventory, isEnemyTurn);

      bool hasSkills = isPlayerTurn && activeCombatant?.GetComponent<SkillComponent>() != null;
      SetButtonState(_skillButton, hasSkills, isEnemyTurn);
    }

    public void SetAutoAllState(bool allAutoEnabled, bool hasPlayerCombatants) {
      if (_autoAllButton == null) {
        return;
      }

      _autoAllButton.text = allAutoEnabled ? "Manual All" : "Auto All";
      if (allAutoEnabled) {
        _autoAllButton.AddToClassList("is-active");
      } else {
        _autoAllButton.RemoveFromClassList("is-active");
      }

      _autoAllButton.SetEnabled(hasPlayerCombatants);
      _autoAllButton.tooltip = !hasPlayerCombatants
        ? "No player combatants available."
        : allAutoEnabled
          ? "Click to disable gambits for all player combatants."
          : "Click to enable gambits for all player combatants.";
    }

    public void ShowItemModal(IEnumerable<ItemData> items, string emptyMessage = null) {
      if (_itemModal == null || _itemList == null) {
        return;
      }

      _itemSource.Clear();
      if (items != null) {
        _itemSource.AddRange(items.Where(static item => item != null));
      }

      _itemList.itemsSource = _itemSource;
      _itemList.RefreshItems();
      _itemList.ClearSelection();

      bool hasItems = _itemSource.Count > 0;

      if (_itemEmptyMessage != null) {
        if (hasItems) {
          _itemEmptyMessage.AddToClassList("is-hidden");
        } else {
          _itemEmptyMessage.text = string.IsNullOrWhiteSpace(emptyMessage)
            ? DefaultItemEmptyMessage
            : emptyMessage.Trim();
          _itemEmptyMessage.RemoveFromClassList("is-hidden");
        }
      }

      if (hasItems) {
        _itemList.RemoveFromClassList("is-hidden");
        _itemList.SetEnabled(true);
      } else {
        _itemList.AddToClassList("is-hidden");
        _itemList.SetEnabled(false);
      }

      _itemModal.RemoveFromClassList("is-hidden");
    }

    public void ShowSkillModal(IEnumerable<SkillSO> skills, IReadOnlyDictionary<string, SkillOptionState> stateBySkillId = null) {
      if (_skillModal == null || _skillList == null) {
        return;
      }

      _skillStates.Clear();
      if (stateBySkillId != null) {
        foreach (KeyValuePair<string, SkillOptionState> pair in stateBySkillId) {
          if (!string.IsNullOrEmpty(pair.Key)) {
            _skillStates[pair.Key] = pair.Value;
          }
        }
      }

      _skillSource.Clear();
      if (skills != null) {
        _skillSource.AddRange(skills.Where(static skill => skill != null));
      }

      _skillList.itemsSource = _skillSource;
      _skillList.RefreshItems();
      _skillList.ClearSelection();
      _skillModal.RemoveFromClassList("is-hidden");
    }

    public void HideModals(bool notify = true) {
      _itemModal?.AddToClassList("is-hidden");
      _skillModal?.AddToClassList("is-hidden");
      _itemList?.ClearSelection();
      _skillList?.ClearSelection();

      if (notify) {
        ModalsClosed?.Invoke();
      }
    }

    private void ConfigureListView(ListView listView, Action<VisualElement, int> bindAction, Action<IEnumerable<object>> selectionHandler) {
      if (listView == null) {
        return;
      }

      listView.makeItem = () => new Label { pickingMode = PickingMode.Position };
      listView.bindItem = bindAction;
      listView.selectionChanged += selectionHandler;
    }

    private void BindItemEntry(VisualElement element, int index) {
      if (element is not Label label || index < 0 || index >= _itemSource.Count) {
        return;
      }

      ItemData item = _itemSource[index];
      label.text = item != null ? item.DisplayName : "Unknown Item";
      label.tooltip = item != null ? item.Description : string.Empty;
    }

    private void BindSkillEntry(VisualElement element, int index) {
      if (element is not Label label || index < 0 || index >= _skillSource.Count) {
        return;
      }

      label.SetEnabled(true);

      SkillSO skill = _skillSource[index];
      if (skill == null) {
        label.text = "Unknown Skill";
        label.tooltip = string.Empty;
        return;
      }

      string displayName = skill.DisplayName;
      string tooltip = skill.Description ?? string.Empty;

      if (_skillStates.TryGetValue(skill.SkillId, out SkillOptionState state)) {
        string AppendTooltip(string baseText, string addition) {
          if (string.IsNullOrWhiteSpace(addition)) {
            return baseText;
          }

          return string.IsNullOrWhiteSpace(baseText) ? addition : $"{baseText}\n{addition}";
        }

        if (!state.IsAvailable) {
          if (state.RemainingCooldownTurns > 0) {
            string turnLabel = state.RemainingCooldownTurns == 1 ? "turn" : "turns";
            displayName = $"{displayName} ({state.RemainingCooldownTurns} {turnLabel})";
            string cooldownHint = !string.IsNullOrWhiteSpace(state.UnavailableReason)
              ? state.UnavailableReason
              : $"Available in {state.RemainingCooldownTurns} {turnLabel}.";
            tooltip = AppendTooltip(tooltip, cooldownHint);
          } else {
            displayName = $"{displayName} (Unavailable)";
            string reason = !string.IsNullOrWhiteSpace(state.UnavailableReason)
              ? state.UnavailableReason
              : "Currently unavailable.";
            tooltip = AppendTooltip(tooltip, reason);
          }

          label.SetEnabled(false);
        } else if (state.RemainingCooldownTurns > 0) {
          // Handle edge case where cooldown is reported but skill is still available (e.g., zero-turn cooldown).
          string turnLabel = state.RemainingCooldownTurns == 1 ? "turn" : "turns";
          string cooldownHint = !string.IsNullOrWhiteSpace(state.UnavailableReason)
            ? state.UnavailableReason
            : $"Ready (cooldown {state.RemainingCooldownTurns} {turnLabel}).";
          tooltip = AppendTooltip(tooltip, cooldownHint);
        }
      }

      label.text = displayName;
      label.tooltip = tooltip;
    }

    private void HandleItemSelectionChanged(IEnumerable<object> selection) {
      ItemData item = selection?.FirstOrDefault() as ItemData;
      if (item == null) {
        return;
      }

      HideModals(false);
      ItemSelected?.Invoke(item);
    }

    private void HandleSkillSelectionChanged(IEnumerable<object> selection) {
      SkillSO skill = selection?.FirstOrDefault() as SkillSO;
      if (skill == null) {
        return;
      }

      if (_skillStates.TryGetValue(skill.SkillId, out SkillOptionState state) && !state.IsAvailable) {
        _skillList?.ClearSelection();
        return;
      }

      HideModals(false);
      SkillSelected?.Invoke(skill);
    }

    private void SetButtonState(Button button, bool isEnabled, bool isEnemyTurn) {
      if (button == null) {
        return;
      }

      button.SetEnabled(isEnabled);

      if (isEnemyTurn) {
        button.AddToClassList(EnemyTurnClass);
      } else {
        button.RemoveFromClassList(EnemyTurnClass);
      }
    }

    private void SetActionButtonsVisibility(bool isVisible) {
      DisplayStyle display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

      if (_attackButton != null) {
        _attackButton.style.display = display;
      }

      if (_defendButton != null) {
        _defendButton.style.display = display;
      }

      if (_itemButton != null) {
        _itemButton.style.display = display;
      }

      if (_skillButton != null) {
        _skillButton.style.display = display;
      }
    }
  }
}
