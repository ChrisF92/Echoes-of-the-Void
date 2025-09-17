using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core;
using EchoesOfTheVoid.UI;
using EchoesOfTheVoid.Combat;
using EchoesOfTheVoid.Combat.Actions;
using UnityEngine;
using UnityEngine.UIElements;

namespace EchoesOfTheVoid.UI.UITK
{
  /// <summary>
  /// Controls the combat UI: loads UXML, binds to <see cref="CombatUIState"/>,
  /// wires commands, and coordinates with <see cref="TurnManager"/> and <see cref="TargetingSystem"/>.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(UIDocument))]
  public sealed class CombatUIController : MonoBehaviour
  {
    [Header("UI Toolkit")]
    [SerializeField] private VisualTreeAsset _combatHudUxml;
    [SerializeField] private UIDocument _uiDocument;

    [Header("Systems")]
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private TargetingSystem _targetingSystem;
    [SerializeField] private EchoesOfTheVoid.Items.ItemManager _itemManager;
    [SerializeField] private EchoesOfTheVoid.Skills.SkillManager _skillManager;
    [SerializeField] private ActionExecutor _actionExecutor;
    [SerializeField] private TargetHighlightView _highlightView;

    [Header("Defaults")] 
    [SerializeField] private int _defaultAttackDamage = 10;
    [SerializeField] private int _defendTurns = 1;
    [SerializeField] private float _defendReduction = 0.5f;

    private CombatUIState _state;

    private Button _attackButton;
    private Button _defendButton;
    private Button _itemButton;
    private Button _skillButton;
    private Button _cancelButton;
    private ListView _itemListView;
    private ListView _skillListView;

    // Keep references so we can raise CanExecuteChanged.
    private UICommand _attackCmd;
    private UICommand _defendCmd;
    private UICommand _itemCmd;
    private UICommand _skillCmd;
    private UICommand _cancelCmd;

    private VisualElement _root;

    private void Awake()
    {
      if (_uiDocument == null)
      {
        _uiDocument = GetComponent<UIDocument>();
      }
      if (_uiDocument != null)
      {
        _uiDocument.sortingOrder = Mathf.Max(_uiDocument.sortingOrder, 100);
      }
      _state = new CombatUIState();
    }

    /// <summary>
    /// Injects references to core systems and managers.
    /// Call during initialization before this component enables, if possible.
    /// </summary>
    public void Configure(
      TurnManager turnManager,
      TargetingSystem targetingSystem,
      EchoesOfTheVoid.Items.ItemManager itemManager,
      EchoesOfTheVoid.Skills.SkillManager skillManager,
      ActionExecutor actionExecutor = null,
      TargetHighlightView highlightView = null)
    {
      _turnManager = turnManager;
      _targetingSystem = targetingSystem;
      _itemManager = itemManager;
      _skillManager = skillManager;
      _actionExecutor = actionExecutor ?? _actionExecutor;
      _highlightView = highlightView ?? _highlightView;
    }

    private void OnEnable()
    {
      BuildUI();
      WireCommands();
      SubscribeSystems();
      SubscribeGrid();
      RefreshCommandsEnabled();
      if (_root != null)
      {
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
      }
    }

    private void OnDisable()
    {
      UnsubscribeSystems();
      UnsubscribeGrid();
      UnwireButtons();
      if (_root != null)
      {
        _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
      }
    }

    private void BuildUI()
    {
      if (_uiDocument == null)
      {
        Debug.LogError("CombatUIController: UIDocument is missing.");
        return;
      }

      _root = _uiDocument.rootVisualElement;
      _root.Clear();

      if (_combatHudUxml != null)
      {
        VisualElement tree = _combatHudUxml.Instantiate();
        _root.Add(tree);
      }
      else
      {
        Debug.LogWarning("CombatUIController: No HUD UXML assigned; UI will be empty.");
      }

      // Attempt to set runtime data source for binding-capable controls.
      TrySetDataSource(_root, _state);

      _attackButton = _root.Q<Button>("attack-button");
      _defendButton = _root.Q<Button>("defend-button");
      _itemButton = _root.Q<Button>("item-button");
      _skillButton = _root.Q<Button>("skill-button");
      _cancelButton = _root.Q<Button>("cancel-button");
      _itemListView = _root.Q<ListView>("item-list");
      _skillListView = _root.Q<ListView>("skill-list");
      // Bind grid to highlight view deterministically.
      _highlightView?.BindToGrid(_root.Q<VisualElement>("combat-grid"), _uiDocument);

      // Initialize list views itemsSource, selection handlers, and hide by default.
      if (_itemListView != null)
      {
        _itemListView.itemsSource = new List<ICombatAction>(_state.AvailableItemActions);
        _itemListView.makeItem = () => new Label();
        _itemListView.bindItem = (e, i) => ((Label)e).text = SafeActionName(_state.AvailableItemActions, i);
        _itemListView.itemsChosen += objects => OnItemChosen(objects);
        _itemListView.selectionChanged += objects => OnItemSelectionChanged(objects);
        _itemListView.style.display = DisplayStyle.None;
      }
      if (_skillListView != null)
      {
        _skillListView.itemsSource = new List<ICombatAction>(_state.AvailableSkillActions);
        _skillListView.makeItem = () => new Label();
        _skillListView.bindItem = (e, i) => ((Label)e).text = SafeActionName(_state.AvailableSkillActions, i);
        _skillListView.itemsChosen += objects => OnSkillChosen(objects);
        _skillListView.selectionChanged += objects => OnSkillSelectionChanged(objects);
        _skillListView.style.display = DisplayStyle.None;
      }
    }

    private void WireCommands()
    {
      _attackCmd = new UICommand(
        execute: () => { HideLists(); OnActionSelected(new AttackAction(_defaultAttackDamage)); },
        canExecute: CanSelectAction);
      _defendCmd = new UICommand(
        execute: () => { HideLists(); ExecuteImmediateAction(new DefendAction(_defendTurns, _defendReduction), targetSelf: true); },
        canExecute: CanSelectAction);
      _itemCmd = new UICommand(
        execute: () => { ShowItemList(); },
        canExecute: CanSelectAction);
      _skillCmd = new UICommand(
        execute: () => { ShowSkillList(); },
        canExecute: CanSelectAction);
      _cancelCmd = new UICommand(
        execute: CancelSelection,
        canExecute: () => HasPendingSelectionOrOpenList());

      _state.AttackCommand = _attackCmd;
      _state.DefendCommand = _defendCmd;
      _state.ItemCommand = _itemCmd;
      _state.SkillCommand = _skillCmd;
      _state.CancelCommand = _cancelCmd;

      if (_attackButton != null) _attackButton.clicked += () => _state.AttackCommand?.Execute();
      if (_defendButton != null) _defendButton.clicked += () => _state.DefendCommand?.Execute();
      if (_itemButton != null) _itemButton.clicked += () => _state.ItemCommand?.Execute();
      if (_skillButton != null) _skillButton.clicked += () => _state.SkillCommand?.Execute();
      if (_cancelButton != null) _cancelButton.clicked += () => _state.CancelCommand?.Execute();
    }

    private void UnwireButtons()
    {
      if (_attackButton != null) _attackButton.clicked -= () => _state.AttackCommand?.Execute();
      if (_defendButton != null) _defendButton.clicked -= () => _state.DefendCommand?.Execute();
      if (_itemButton != null) _itemButton.clicked -= () => _state.ItemCommand?.Execute();
      if (_skillButton != null) _skillButton.clicked -= () => _state.SkillCommand?.Execute();
      if (_cancelButton != null) _cancelButton.clicked -= () => _state.CancelCommand?.Execute();
    }

    private void SubscribeSystems()
    {
      if (_turnManager != null)
      {
        _turnManager.TurnStarted += OnTurnStarted;
        _turnManager.TurnEnded += OnTurnEnded;
        _turnManager.CombatEnded += OnCombatEnded;
      }
    }

    private void UnsubscribeSystems()
    {
      if (_turnManager != null)
      {
        _turnManager.TurnStarted -= OnTurnStarted;
        _turnManager.TurnEnded -= OnTurnEnded;
        _turnManager.CombatEnded -= OnCombatEnded;
      }
    }

    private void SubscribeGrid()
    {
      if (_highlightView != null)
      {
        _highlightView.CellClicked += OnGridCellClicked;
      }
    }

    private void UnsubscribeGrid()
    {
      if (_highlightView != null)
      {
        _highlightView.CellClicked -= OnGridCellClicked;
      }
    }

    private void OnTurnStarted(ICombatant combatant)
    {
      _state.CurrentTurnName = combatant != null ? combatant.Name : string.Empty;
      // Refresh item/skill lists based on current user and targeting.
      if (combatant != null)
      {
        if (_itemManager != null)
        {
          var items = _itemManager.GetUsableItems(combatant, _targetingSystem);
          _state.SetAvailableItemActions(items);
        }
        if (_skillManager != null)
        {
          var skills = _skillManager.GetUsableSkills(combatant, _targetingSystem);
          _state.SetAvailableSkillActions(skills);
        }
        RefreshListViews();
      }
      HideLists();
      RaiseCanExecuteChanged();
      RefreshCommandsEnabled();
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
      if (evt == null)
      {
        return;
      }
      if (evt.keyCode == UnityEngine.KeyCode.Escape)
      {
        CancelSelection();
        evt.StopPropagation();
      }
    }

    private void OnTurnEnded(ICombatant combatant)
    {
      // Clear selection/highlights when a turn ends.
      _state.SelectedAction = null;
      _state.SetValidTargets(Array.Empty<ICombatant>());
      _state.SetAvailableItemActions(Array.Empty<ICombatAction>());
      _state.SetAvailableSkillActions(Array.Empty<ICombatAction>());
      if (_targetingSystem != null)
      {
        _targetingSystem.ClearHighlights();
      }
      RefreshListViews();
      HideLists();
      RaiseCanExecuteChanged();
      RefreshCommandsEnabled();
    }

    private void OnCombatEnded()
    {
      _state.Clear();
      if (_targetingSystem != null)
      {
        _targetingSystem.ClearHighlights();
      }
      RefreshListViews();
      HideLists();
      RaiseCanExecuteChanged();
      RefreshCommandsEnabled();
    }

    private void OnActionSelected(ICombatAction action)
    {
      if (action == null)
      {
        return;
      }

      Debug.Log($"[UI] Action selected: {action.Name}");
      _state.SelectedAction = action;

      if (_turnManager == null || _targetingSystem == null)
      {
        return;
      }

      ICombatant user = _turnManager.CurrentCombatant;
      if (user == null)
      {
        Debug.LogWarning("[UI] No current combatant when selecting action.");
        return;
      }

      List<ICombatant> targets = _targetingSystem.GetValidTargets(action, user);
      Debug.Log($"[UI] Valid targets for {action.Name}: {targets?.Count ?? 0}");
      _state.SetValidTargets(targets);
      _targetingSystem.HighlightTargets(targets);
      RefreshCommandsEnabled();
    }

    private void OnGridCellClicked(ICombatant clicked)
    {
      // Only act if there's a pending actionable selection and clicked is a valid target.
      var pending = _state.SelectedAction;
      if (pending == null || clicked == null)
      {
        Debug.Log($"[UI] Grid click ignored. Pending={pending != null}, Target={(clicked != null ? clicked.Name : "null")}");
        return;
      }
      if (_turnManager == null || _actionExecutor == null)
      {
        Debug.LogWarning("[UI] Missing TurnManager or ActionExecutor; cannot execute action.");
        return;
      }
      // Validate target is in current valid list.
      bool isValid = false;
      var list = _state.ValidTargets;
      for (int i = 0; i < list.Count; i++)
      {
        if (ReferenceEquals(list[i], clicked))
        {
          isValid = true;
          break;
        }
      }
      if (!isValid)
      {
        Debug.Log($"[UI] Clicked target {clicked.Name} is not in valid targets for {pending.Name}.");
        return;
      }

      var user = _turnManager.CurrentCombatant;
      if (user == null)
      {
        Debug.LogWarning("[UI] No current combatant when executing action.");
        return;
      }

      Debug.Log($"[UI] Executing {pending.Name}: {user.Name} -> {clicked.Name}");
      _actionExecutor.ExecuteAction(pending, user, clicked);
      // Reset selection UI state.
      _state.SelectedAction = null;
      _state.SetValidTargets(Array.Empty<ICombatant>());
      HideLists();
      RefreshCommandsEnabled();
    }

    private void ExecuteImmediateAction(ICombatAction action, bool targetSelf)
    {
      if (action == null || _turnManager == null || _actionExecutor == null)
      {
        return;
      }
      var user = _turnManager.CurrentCombatant;
      if (user == null)
      {
        return;
      }
      var target = targetSelf ? user : null;
      _actionExecutor.ExecuteAction(action, user, target);
      HideLists();
      _state.SelectedAction = null;
      _state.SetValidTargets(Array.Empty<ICombatant>());
      RefreshCommandsEnabled();
    }

    private bool CanSelectAction()
    {
      if (_turnManager == null)
      {
        return true;
      }
      if (!_turnManager.IsCombatActive)
      {
        return false;
      }
      ICombatant current = _turnManager.CurrentCombatant;
      if (current == null)
      {
        return false;
      }
      // Enable player-only UI when it's a player character's turn.
      return current is PlayerCharacter;
    }

    private void RaiseCanExecuteChanged()
    {
      _attackCmd?.RaiseCanExecuteChanged();
      _defendCmd?.RaiseCanExecuteChanged();
      _itemCmd?.RaiseCanExecuteChanged();
      _skillCmd?.RaiseCanExecuteChanged();
    }

    private void RefreshCommandsEnabled()
    {
      bool can = CanSelectAction();
      if (_attackButton != null) _attackButton.SetEnabled(can);
      if (_defendButton != null) _defendButton.SetEnabled(can);
      if (_itemButton != null) _itemButton.SetEnabled(can);
      if (_skillButton != null) _skillButton.SetEnabled(can);
      if (_cancelButton != null) _cancelButton.SetEnabled(HasPendingSelectionOrOpenList());
      _cancelCmd?.RaiseCanExecuteChanged();
    }

    private void RefreshListViews()
    {
      if (_itemListView != null)
      {
        _itemListView.itemsSource = new List<ICombatAction>(_state.AvailableItemActions);
        _itemListView.RefreshItems();
      }
      if (_skillListView != null)
      {
        _skillListView.itemsSource = new List<ICombatAction>(_state.AvailableSkillActions);
        _skillListView.RefreshItems();
      }
    }

    private static string SafeActionName(IReadOnlyList<ICombatAction> list, int index)
    {
      if (list == null || index < 0 || index >= list.Count)
      {
        return string.Empty;
      }
      return list[index]?.Name ?? string.Empty;
    }

    private bool HasPendingSelectionOrOpenList()
    {
      bool listOpen = (_itemListView != null && _itemListView.style.display == DisplayStyle.Flex)
        || (_skillListView != null && _skillListView.style.display == DisplayStyle.Flex);
      return listOpen || _state.SelectedAction != null;
    }

    private void CancelSelection()
    {
      HideLists();
      _state.SelectedAction = null;
      _state.SetValidTargets(Array.Empty<ICombatant>());
      if (_targetingSystem != null)
      {
        _targetingSystem.ClearHighlights();
      }
    }

    private void ShowItemList()
    {
      if (_itemListView != null)
      {
        _itemListView.style.display = DisplayStyle.Flex;
      }
      if (_skillListView != null)
      {
        _skillListView.style.display = DisplayStyle.None;
      }
      RefreshCommandsEnabled();
    }

    private void ShowSkillList()
    {
      if (_skillListView != null)
      {
        _skillListView.style.display = DisplayStyle.Flex;
      }
      if (_itemListView != null)
      {
        _itemListView.style.display = DisplayStyle.None;
      }
      RefreshCommandsEnabled();
    }

    private void HideLists()
    {
      if (_itemListView != null)
      {
        _itemListView.style.display = DisplayStyle.None;
      }
      if (_skillListView != null)
      {
        _skillListView.style.display = DisplayStyle.None;
      }
      RefreshCommandsEnabled();
    }

    private void OnItemChosen(System.Collections.Generic.IEnumerable<object> chosen)
    {
      var enumerator = chosen?.GetEnumerator();
      if (enumerator != null && enumerator.MoveNext())
      {
        if (enumerator.Current is ICombatAction action)
        {
          HideLists();
          OnActionSelected(action);
        }
      }
    }

    private void OnItemSelectionChanged(System.Collections.Generic.IEnumerable<object> selected)
    {
      var enumerator = selected?.GetEnumerator();
      if (enumerator != null && enumerator.MoveNext())
      {
        if (enumerator.Current is ICombatAction action)
        {
          // Treat selection as pending action so grid clicks can execute it.
          _state.SelectedAction = action;
          if (_turnManager != null && _targetingSystem != null)
          {
            var user = _turnManager.CurrentCombatant;
            if (user != null)
            {
              var targets = _targetingSystem.GetValidTargets(action, user);
              _state.SetValidTargets(targets);
              _targetingSystem.HighlightTargets(targets);
            }
          }
        }
      }
    }

    private void OnSkillChosen(System.Collections.Generic.IEnumerable<object> chosen)
    {
      var enumerator = chosen?.GetEnumerator();
      if (enumerator != null && enumerator.MoveNext())
      {
        if (enumerator.Current is ICombatAction action)
        {
          HideLists();
          OnActionSelected(action);
        }
      }
    }

    private void OnSkillSelectionChanged(System.Collections.Generic.IEnumerable<object> selected)
    {
      var enumerator = selected?.GetEnumerator();
      if (enumerator != null && enumerator.MoveNext())
      {
        if (enumerator.Current is ICombatAction action)
        {
          // Treat selection as pending action so grid clicks can execute it.
          _state.SelectedAction = action;
          if (_turnManager != null && _targetingSystem != null)
          {
            var user = _turnManager.CurrentCombatant;
            if (user != null)
            {
              var targets = _targetingSystem.GetValidTargets(action, user);
              _state.SetValidTargets(targets);
              _targetingSystem.HighlightTargets(targets);
            }
          }
        }
      }
    }

    private static void TrySetDataSource(VisualElement root, object dataSource)
    {
      try
      {
        var prop = typeof(VisualElement).GetProperty("dataSource");
        if (prop != null && prop.CanWrite)
        {
          prop.SetValue(root, dataSource);
        }
      }
      catch
      {
        // Ignore if runtime binding API is not available in this Unity version.
      }
    }

    private sealed class NoopItemEffect : IItemEffect
    {
      public void Apply(ICombatant user, ICombatant target) { }
    }

    private sealed class NoopSkillEffect : ISkillEffect
    {
      public int ManaCost => 0;
      public void Execute(ICombatant user, ICombatant target) { }
    }
  }
}
