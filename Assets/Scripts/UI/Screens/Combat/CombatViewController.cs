using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Results;
using ItemData = EchoesOfTheVoid.Core.Inventory.ScriptableObjects.ItemScriptableObject;

namespace EchoesOfTheVoid.UI.Combat {
  public enum MessageType {
    Normal,
    Damage,
    Healing,
    System
  }

  [Serializable]
  public class CombatantUIData {
    [CreateProperty] public string Name { get; private set; }
    [CreateProperty] public int CurrentHP { get; private set; }
    [CreateProperty] public int MaxHP { get; private set; }
    [CreateProperty] public bool IsAlive { get; private set; }
    [CreateProperty] public Vector2Int GridPosition { get; private set; }
    [CreateProperty] public bool IsPlayerControlled { get; private set; }
    [CreateProperty] public Sprite Portrait { get; private set; }
    [CreateProperty] public bool IsDefending { get; private set; }
    [CreateProperty] public bool IsDefendingThisTurn { get; private set; }
    [CreateProperty] public bool IsTargetable { get; private set; } = true;

    public Combatant SourceCombatant { get; private set; }

    [CreateProperty]
    public float HPPercentage => MaxHP > 0 ? Mathf.Clamp01((float)CurrentHP / MaxHP) : 0f;

    public CombatantUIData(Combatant combatant, Vector2Int gridPos) {
      GridPosition = gridPos;
      UpdateFromCombatant(combatant);
    }

    public void UpdateFromCombatant(Combatant combatant, Vector2Int? gridPosOverride = null, Sprite portraitOverride = null) {
      SourceCombatant = combatant;

      if (combatant == null) {
        Name = string.Empty;
        CurrentHP = 0;
        MaxHP = 0;
        IsAlive = false;
        IsPlayerControlled = true;
        IsDefending = false;
        IsTargetable = false;
        Portrait = null;
        return;
      }

      Name = combatant.Name;
      CurrentHP = combatant.GetStat(StatType.Health);
      MaxHP = combatant.GetMaxStat(StatType.Health);
      IsAlive = combatant.IsAlive;
      IsPlayerControlled = combatant.IsPlayerControlled;
      IsDefending = combatant.IsDefending;
      IsTargetable = combatant.IsAlive;

      if (gridPosOverride.HasValue) {
        GridPosition = gridPosOverride.Value;
      }

      if (portraitOverride != null) {
        Portrait = portraitOverride;
      }
    }

    public void SetPortrait(Sprite portrait) {
      Portrait = portrait;
    }

    public void SetDefendingState(bool defending) {
      IsDefending = defending;
      IsDefendingThisTurn = defending;
    }
  }

  [Serializable]
  public class CombatUIData : INotifyValueChanged<int> {
    [CreateProperty] public int TurnNumber { get; private set; }
    [CreateProperty] public string BattleTimer { get; private set; } = "00:00";
    [CreateProperty] public string CurrentActionText { get; private set; } = string.Empty;
    [CreateProperty] public string CurrentTurnCharacter { get; private set; } = string.Empty;
    [CreateProperty] public bool IsPlayerTurn { get; private set; }

    public event Action<int> ValueChanged;

    public void Reset() {
      TurnNumber = 1;
      BattleTimer = "00:00";
      CurrentActionText = string.Empty;
      CurrentTurnCharacter = string.Empty;
      IsPlayerTurn = false;
      ValueChanged?.Invoke(TurnNumber);
    }

    public void IncrementTurn() {
      TurnNumber++;
      ValueChanged?.Invoke(TurnNumber);
    }

    public void SetTurnInfo(Combatant combatant) {
      CurrentTurnCharacter = combatant != null ? combatant.Name : string.Empty;
      IsPlayerTurn = combatant != null && combatant.IsPlayerControlled;
    }

    public void SetActionText(string actionText) {
      CurrentActionText = actionText;
    }

    public void SetBattleTimer(string timerText) {
      BattleTimer = timerText;
    }

    public int value {
      get => TurnNumber;
      set {
        TurnNumber = value;
        ValueChanged?.Invoke(TurnNumber);
      }
    }

    public void SetTurnNumber(int turnNumber) {
      TurnNumber = Mathf.Max(1, turnNumber);
      ValueChanged?.Invoke(TurnNumber);
    }

    public void SetValueWithoutNotify(int newValue) {
      throw new NotImplementedException();
    }
  }

  [DisallowMultipleComponent]
  [RequireComponent(typeof(UIDocument))]
  public class CombatViewController : MonoBehaviour {
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private CombatUIData _combatUIData = new();
    [SerializeField] private CombatSystem _combatSystem;

    private VisualElement _rootElement;
    private VisualElement _playerGrid;
    private VisualElement _enemyGrid;

    private readonly List<VisualElement> _playerSlots = new(9);
    private readonly List<VisualElement> _enemySlots = new(9);

    private readonly List<Combatant> _playerTeam = new();
    private readonly List<Combatant> _enemyTeam = new();

    private readonly Dictionary<Combatant, CombatantUIData> _combatantUIData = new();
    private readonly Dictionary<Combatant, VisualElement> _combatantSlots = new();
    private readonly Dictionary<Combatant, Vector2Int> _combatantGridPositions = new();
    private readonly Dictionary<VisualElement, SlotVisualCache> _slotCache = new();
    private readonly Dictionary<Combatant, CombatantEventSubscription> _combatantEventSubscriptions = new();
    private readonly Dictionary<Combatant, CombatantTemplateScriptableObject> _combatantTemplates = new();

    private bool _isSelectingTarget;
    private CombatActionType _currentAction = CombatActionType.Attack;
    private readonly List<VisualElement> _validTargets = new();
    private Combatant _selectedCombatant;
    private Combatant _currentTurnCombatant;

    private VisualElement _itemModal;
    private VisualElement _skillModal;
    private ListView _itemList;
    private ListView _skillList;

    private Button _attackBtn;
    private Button _defendBtn;
    private Button _itemBtn;
    private Button _skillBtn;
    private Button _itemModalCloseBtn;
    private Button _skillModalCloseBtn;

    private Label _turnCounterLabel;
    private Label _battleTimerLabel;
    private Label _currentActionLabel;
    private Label _currentTurnLabel;

    private ScrollView _combatLogScrollView;
    private VisualElement _combatLogContainer;
    private readonly List<Label> _activeLogEntries = new();
    private readonly Stack<Label> _logEntryPool = new();
    private const int _maxLogEntries = 60;

    private readonly WaitForSeconds _logScrollDelay = new(0.01f);

    private float _battleTimerSeconds;
    private Coroutine _battleTimerRoutine;

    private SkillScriptableObject _pendingSkill;
    private ItemData _pendingItem;

    private readonly List<ItemData> _itemSource = new();
    private readonly List<SkillScriptableObject> _skillSource = new();

    private bool _isInitialized;

    private void Awake() {
      if (_uiDocument == null) {
        _uiDocument = GetComponent<UIDocument>();
      }

      if (_uiDocument == null) {
        Debug.LogError("CombatViewController requires a UIDocument reference.", this);
        enabled = false;
        return;
      }

      _rootElement = _uiDocument.rootVisualElement;
      if (_rootElement == null) {
        Debug.LogError("CombatViewController failed to locate root visual element.", this);
        enabled = false;
        return;
      }

      if (_combatSystem == null) {
        _combatSystem = CombatSystem.Instance;
      }

      SetupUIReferences();
      InitializeCombatGrids();
      SetupDataBinding();
      _isInitialized = true;
    }

    private void Start() {
      if (!_isInitialized) {
        return;
      }

      SetupButtonEvents();
      HideModals();
      UpdateTurnInfoUI();
    }

    private void OnEnable() {
      if (_combatSystem != null) {
        _combatSystem.OnTurnStart += HandleCombatTurnStart;
        _combatSystem.OnTurnEnd += HandleCombatTurnEnd;
        _combatSystem.OnActionExecuted += HandleActionExecuted;
      }
    }

    private void OnDisable() {
      if (_combatSystem != null) {
        _combatSystem.OnTurnStart -= HandleCombatTurnStart;
        _combatSystem.OnTurnEnd -= HandleCombatTurnEnd;
        _combatSystem.OnActionExecuted -= HandleActionExecuted;
      }
    }

    private void OnDestroy() {
      StopBattleTimer();
      UnsubscribeFromAllCombatantEvents();
    }

    public void InitializeBattle(List<Combatant> players, List<Combatant> enemies) {
      if (!_isInitialized) {
        Debug.LogWarning("CombatViewController not initialized before InitializeBattle call.", this);
        return;
      }

      ResetViewState();
      SetPlayerTeam(players);
      SetEnemyTeam(enemies);
      StartBattleTimer();

      if (_combatSystem != null && _combatSystem.CurrentTurnCombatant is Combatant combatant) {
        SetCurrentTurnCombatant(combatant);
      }
    }

    public void SetActivePlayer(Combatant combatant) {
      SetCurrentTurnCombatant(combatant);
    }

    public void HandleCombatAction(CombatActionType actionType, Combatant caster, Combatant target, object actionData = null) {
      ExecuteAction(actionType, caster, target, actionData);
    }

    private void SetupUIReferences() {
      _playerGrid = _rootElement.Q<VisualElement>("player-grid");
      _enemyGrid = _rootElement.Q<VisualElement>("enemy-grid");

      _attackBtn = _rootElement.Q<Button>("attack-btn");
      _defendBtn = _rootElement.Q<Button>("defend-btn");
      _itemBtn = _rootElement.Q<Button>("item-btn");
      _skillBtn = _rootElement.Q<Button>("skill-btn");
      _itemModalCloseBtn = _rootElement.Q<Button>("item-close-btn");
      _skillModalCloseBtn = _rootElement.Q<Button>("skill-close-btn");

      _itemModal = _rootElement.Q<VisualElement>("item-modal");
      _skillModal = _rootElement.Q<VisualElement>("skill-modal");
      _itemList = _rootElement.Q<ListView>("item-list");
      _skillList = _rootElement.Q<ListView>("skill-list");

      _combatLogScrollView = _rootElement.Q<ScrollView>("combat-log");
      _combatLogContainer = _rootElement.Q<VisualElement>("log-messages");

      _turnCounterLabel = _rootElement.Q<Label>("turn-counter");
      _battleTimerLabel = _rootElement.Q<Label>("battle-timer");
      _currentActionLabel = _rootElement.Q<Label>("current-action");
      _currentTurnLabel = _rootElement.Q<Label>("current-turn-character");

      ConfigureListView(_itemList, _itemSource, BindItemEntry, HandleItemSelectionChanged);
      ConfigureListView(_skillList, _skillSource, BindSkillEntry, HandleSkillSelectionChanged);
    }

    private void SetupDataBinding() {
      _combatUIData ??= new CombatUIData();

      _rootElement.dataSource = _combatUIData;
      UpdateTurnInfoUI();
    }

    private void SetupButtonEvents() {
      if (_attackBtn != null) {
        _attackBtn.clicked += () => OnActionButtonClicked(CombatActionType.Attack);
      }

      if (_defendBtn != null) {
        _defendBtn.clicked += () => OnActionButtonClicked(CombatActionType.Defend);
      }

      if (_itemBtn != null) {
        _itemBtn.clicked += () => OnActionButtonClicked(CombatActionType.Item);
      }

      if (_skillBtn != null) {
        _skillBtn.clicked += () => OnActionButtonClicked(CombatActionType.Skill);
      }

      if (_itemModalCloseBtn != null) {
        _itemModalCloseBtn.clicked += HideModals;
      }

      if (_skillModalCloseBtn != null) {
        _skillModalCloseBtn.clicked += HideModals;
      }
    }

    private void InitializeCombatGrids() {
      _playerSlots.Clear();
      _enemySlots.Clear();

      if (_playerGrid != null) {
        for (int index = 0; index < 9; index++) {
          VisualElement slot = _playerGrid.Q<VisualElement>($"player-slot-{index}");
          if (slot != null) {
            CacheSlot(slot, new Vector2Int(index % 3, index / 3), true);
            _playerSlots.Add(slot);
          }
        }
      }

      if (_enemyGrid != null) {
        for (int index = 0; index < 9; index++) {
          VisualElement slot = _enemyGrid.Q<VisualElement>($"enemy-slot-{index}");
          if (slot != null) {
            CacheSlot(slot, new Vector2Int(index % 3, index / 3), false);
            _enemySlots.Add(slot);
          }
        }
      }
    }

    private void CacheSlot(VisualElement slot, Vector2Int gridPos, bool isPlayerTeam) {
      if (_slotCache.ContainsKey(slot)) {
        return;
      }

      var cache = new SlotVisualCache {
        Root = slot,
        GridPosition = gridPos,
        IsPlayerSlot = isPlayerTeam,
        NameLabel = slot.Q<Label>(className: "combatant-name"),
        HealthBar = slot.Q<ProgressBar>(className: "health-bar"),
        Portrait = slot.Q<VisualElement>(className: "portrait")
      };

      _slotCache.Add(slot, cache);

      slot.RegisterCallback<ClickEvent>(_ => {
        OnCombatantSlotClicked(slot, cache.Combatant);
      });
    }

    private void SetPlayerTeam(List<Combatant> combatants) {
      ClearTeamGrid(_playerSlots);
      _playerTeam.Clear();

      if (combatants == null) {
        return;
      }

      for (int i = 0; i < combatants.Count && i < _playerSlots.Count; i++) {
        Combatant combatant = combatants[i];
        VisualElement slot = _playerSlots[i];
        Vector2Int gridPos = _slotCache.TryGetValue(slot, out SlotVisualCache cache) ? cache.GridPosition : new Vector2Int(i % 3, i / 3);
        AddCombatantToGridInternal(combatant, slot, gridPos);
        _playerTeam.Add(combatant);
      }
    }

    private void SetEnemyTeam(List<Combatant> combatants) {
      ClearTeamGrid(_enemySlots);
      _enemyTeam.Clear();

      if (combatants == null) {
        return;
      }

      for (int i = 0; i < combatants.Count && i < _enemySlots.Count; i++) {
        Combatant combatant = combatants[i];
        VisualElement slot = _enemySlots[i];
        Vector2Int gridPos = _slotCache.TryGetValue(slot, out SlotVisualCache cache) ? cache.GridPosition : new Vector2Int(i % 3, i / 3);
        AddCombatantToGridInternal(combatant, slot, gridPos);
        _enemyTeam.Add(combatant);
      }
    }

    public void AddCombatantToGrid(Combatant combatant, Vector2Int gridPosition) {
      if (combatant == null) {
        Debug.LogWarning("Attempted to add a null combatant to the grid.", this);
        return;
      }

      List<VisualElement> slotList = combatant.IsPlayerControlled ? _playerSlots : _enemySlots;
      VisualElement slot = GetSlotAtPosition(slotList, gridPosition);

      if (slot == null) {
        Debug.LogWarning($"Invalid grid position {gridPosition} for combatant {combatant.name}.", this);
        return;
      }

      AddCombatantToGridInternal(combatant, slot, gridPosition);

      if (combatant.IsPlayerControlled && !_playerTeam.Contains(combatant)) {
        _playerTeam.Add(combatant);
      } else if (!combatant.IsPlayerControlled && !_enemyTeam.Contains(combatant)) {
        _enemyTeam.Add(combatant);
      }
    }

    private void AddCombatantToGridInternal(Combatant combatant, VisualElement slot, Vector2Int gridPosition) {
      if (combatant == null || slot == null) {
        return;
      }

      _combatantGridPositions[combatant] = gridPosition;
      _combatantSlots[combatant] = slot;

      if (!_combatantUIData.TryGetValue(combatant, out CombatantUIData uiData)) {
        uiData = new CombatantUIData(combatant, gridPosition);
        _combatantUIData[combatant] = uiData;
      } else {
        uiData.UpdateFromCombatant(combatant, gridPosition);
      }

      if (_combatantTemplates.TryGetValue(combatant, out CombatantTemplateScriptableObject template) && template != null) {
        uiData.SetPortrait(template.portrait);
      }

      PopulateCombatantSlot(slot, combatant, gridPosition);
      SetupCombatantEventListeners(combatant);
    }

    public void RemoveCombatantFromGrid(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      if (_combatantSlots.TryGetValue(combatant, out VisualElement slot)) {
        ClearCombatantSlot(slot);
      }

      _ = _combatantSlots.Remove(combatant);
      _ = _combatantGridPositions.Remove(combatant);
      _ = _combatantUIData.Remove(combatant);
      _ = _playerTeam.Remove(combatant);
      _ = _enemyTeam.Remove(combatant);

      UnsubscribeCombatantEvents(combatant);
    }

    public Vector2Int GetGridPosition(Combatant combatant) {
      return _combatantGridPositions.TryGetValue(combatant, out Vector2Int position) ? position : new Vector2Int(-1, -1);
    }

    private void PopulateCombatantSlot(VisualElement slot, Combatant combatant, Vector2Int gridPos) {
      if (slot == null) {
        return;
      }

      if (!_slotCache.TryGetValue(slot, out SlotVisualCache cache)) {
        return;
      }

      cache.Combatant = combatant;

      slot.RemoveFromClassList("is-empty");
      slot.AddToClassList("combatant-alive");
      slot.RemoveFromClassList("combatant-dead");

      if (_combatantUIData.TryGetValue(combatant, out CombatantUIData uiData)) {
        ApplyCombatantDataToSlot(cache, uiData);
      }
    }

    private void UpdateCombatantDisplay(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      if (!_combatantUIData.TryGetValue(combatant, out CombatantUIData uiData)) {
        uiData = new CombatantUIData(combatant, GetGridPosition(combatant));
        _combatantUIData[combatant] = uiData;
      } else {
        Sprite portrait = _combatantTemplates.TryGetValue(combatant, out CombatantTemplateScriptableObject template) ? template.portrait : uiData.Portrait;
        uiData.UpdateFromCombatant(combatant, GetGridPosition(combatant), portrait);
      }

      if (!_combatantSlots.TryGetValue(combatant, out VisualElement slot)) {
        return;
      }

      if (!_slotCache.TryGetValue(slot, out SlotVisualCache cache)) {
        return;
      }

      ApplyCombatantDataToSlot(cache, uiData);
      ApplyTargetHighlighting();
    }

    private void ApplyCombatantDataToSlot(SlotVisualCache cache, CombatantUIData uiData) {
      if (cache.NameLabel != null) {
        cache.NameLabel.bindingPath = nameof(CombatantUIData.Name);
        cache.NameLabel.dataSource = uiData;
        cache.NameLabel.text = uiData.Name;
      }

      if (cache.HealthBar != null) {
        cache.HealthBar.bindingPath = nameof(CombatantUIData.HPPercentage);
        cache.HealthBar.dataSource = uiData;
        cache.HealthBar.lowValue = 0f;
        cache.HealthBar.highValue = 1f;
        cache.HealthBar.value = uiData.HPPercentage;
        cache.HealthBar.title = $"{uiData.CurrentHP}/{uiData.MaxHP}";
      }

      if (cache.Portrait != null) {
        if (uiData.Portrait != null) {
          cache.Portrait.style.backgroundImage = new StyleBackground(uiData.Portrait);
          cache.Portrait.RemoveFromClassList("portrait-empty");
        } else {
          cache.Portrait.style.backgroundImage = StyleKeyword.Null;
          cache.Portrait.AddToClassList("portrait-empty");
        }
      }

      if (cache.Root != null) {
        if (uiData.IsAlive) {
          cache.Root.AddToClassList("combatant-alive");
          cache.Root.RemoveFromClassList("combatant-dead");
        } else {
          cache.Root.AddToClassList("combatant-dead");
          cache.Root.RemoveFromClassList("combatant-alive");
        }

        if (uiData.IsDefending) {
          cache.Root.AddToClassList("combatant-defending");
        } else {
          cache.Root.RemoveFromClassList("combatant-defending");
        }
      }
    }

    private void ClearCombatantSlot(VisualElement slot) {
      if (slot == null || !_slotCache.TryGetValue(slot, out SlotVisualCache cache)) {
        return;
      }

      cache.Combatant = null;

      slot.AddToClassList("is-empty");
      slot.RemoveFromClassList("combatant-alive");
      slot.RemoveFromClassList("combatant-dead");
      slot.RemoveFromClassList("valid-target");
      slot.RemoveFromClassList("invalid-target");
      slot.RemoveFromClassList("selected-target");

      if (cache.NameLabel != null) {
        cache.NameLabel.text = "-";
      }

      if (cache.HealthBar != null) {
        cache.HealthBar.value = 0f;
        cache.HealthBar.title = string.Empty;
      }

      if (cache.Portrait != null) {
        cache.Portrait.style.backgroundImage = StyleKeyword.Null;
        cache.Portrait.AddToClassList("portrait-empty");
      }
    }

    private void OnActionButtonClicked(CombatActionType actionType) {
      if (_currentTurnCombatant == null) {
        AddCombatMessage("No active combatant.", MessageType.System);
        return;
      }

      _currentAction = actionType;
      _combatUIData.SetActionText(actionType.ToString());
      UpdateTurnInfoUI();

      switch (actionType) {
        case CombatActionType.Attack:
          StartTargetSelection(actionType);
          break;
        case CombatActionType.Defend:
          ExecuteAction(CombatActionType.Defend, _currentTurnCombatant, _currentTurnCombatant);
          break;
        case CombatActionType.Item:
          ShowItemModal(_currentTurnCombatant);
          break;
        case CombatActionType.Skill:
          ShowSkillModal(_currentTurnCombatant);
          break;
        default:
          break;
      }
    }

    private void StartTargetSelection(CombatActionType actionType) {
      if (_currentTurnCombatant == null) {
        return;
      }

      _isSelectingTarget = true;
      HighlightValidTargets(actionType, _currentTurnCombatant);
    }

    private void EndTargetSelection() {
      _isSelectingTarget = false;
      _selectedCombatant = null;
      _validTargets.Clear();

      foreach (SlotVisualCache cache in _slotCache.Values) {
        cache.Root.RemoveFromClassList("valid-target");
        cache.Root.RemoveFromClassList("invalid-target");
        cache.Root.RemoveFromClassList("selected-target");
      }
    }

    private void HighlightValidTargets(CombatActionType actionType, Combatant caster) {
      if (caster == null) {
        return;
      }

      List<Combatant> validCombatants = GetValidTargets(actionType, caster);
      _validTargets.Clear();

      foreach (SlotVisualCache cache in _slotCache.Values) {
        cache.Root.RemoveFromClassList("valid-target");
        cache.Root.RemoveFromClassList("invalid-target");
        cache.Root.RemoveFromClassList("selected-target");

        if (cache.Combatant == null || !cache.Combatant.IsAlive) {
          continue;
        }

        if (validCombatants.Contains(cache.Combatant)) {
          cache.Root.AddToClassList("valid-target");
          _validTargets.Add(cache.Root);
        } else {
          cache.Root.AddToClassList("invalid-target");
        }
      }
    }

    private void OnCombatantSlotClicked(VisualElement slot, Combatant combatant) {
      if (!_isSelectingTarget) {
        return;
      }

      if (combatant == null) {
        AddCombatMessage("Cannot target an empty slot.", MessageType.System);
        return;
      }

      if (!IsValidTarget(combatant, _currentAction, _currentTurnCombatant)) {
        AddCombatMessage($"{combatant.Name} is not a valid target.", MessageType.System);
        return;
      }

      _selectedCombatant = combatant;

      foreach (SlotVisualCache cache in _slotCache.Values) {
        cache.Root.RemoveFromClassList("selected-target");
      }

      slot?.AddToClassList("selected-target");

      ExecuteAction(_currentAction, _currentTurnCombatant, combatant, _pendingSkill != null ? _pendingSkill : _pendingItem);
      EndTargetSelection();
    }

    public void ExecuteAction(CombatActionType actionType, Combatant caster, Combatant target, object actionData = null) {
      if (caster == null) {
        Debug.LogWarning("ExecuteAction called without a caster.", this);
        return;
      }

      var combatAction = new CombatAction {
        ActionType = actionType,
        Target = target
      };

      switch (actionType) {
        case CombatActionType.Attack:
          if (target == null) {
            AddCombatMessage("Attack requires a target.", MessageType.System);
            return;
          }
          break;
        case CombatActionType.Defend:
          combatAction.Target = caster;
          caster.SetDefending(true);
          break;
        case CombatActionType.Item:
          ItemData item = actionData as ItemData ?? _pendingItem;
          if (item == null) {
            AddCombatMessage("Select an item first.", MessageType.System);
            return;
          }
          combatAction.ItemData = item;
          if (target == null) {
            combatAction.Target = caster;
          }
          break;
        case CombatActionType.Skill:
          SkillScriptableObject skill = actionData as SkillScriptableObject ?? _pendingSkill;
          if (skill == null) {
            AddCombatMessage("Select a skill first.", MessageType.System);
            return;
          }
          combatAction.SkillId = skill.SkillId;
          if (target == null && RequiresTarget(skill)) {
            AddCombatMessage("Skill requires a target.", MessageType.System);
            return;
          }
          if (!RequiresTarget(skill)) {
            combatAction.Target = caster;
          }
          break;
        default:
          break;
      }

      _combatUIData.SetActionText(actionType.ToString());
      UpdateTurnInfoUI();

      if (_combatSystem != null) {
        bool success = _combatSystem.ExecuteAction(caster, combatAction);
        if (!success) {
          AddCombatMessage("Action could not be executed.", MessageType.System);
        }
      } else {
        ApplyLocalActionSimulation(caster, combatAction);
      }

      _pendingItem = null;
      _pendingSkill = null;
      HideModals();
    }

    private void ApplyLocalActionSimulation(Combatant caster, CombatAction action) {
      switch (action.ActionType) {
        case CombatActionType.Attack:
          if (action.Target is Combatant targetCombatant) {
            targetCombatant.TakeDamage(Mathf.Max(1, caster.GetStat(StatType.Attack) - targetCombatant.GetStat(StatType.Defense)));
            AddCombatMessage($"{caster.Name} attacks {targetCombatant.Name}.", MessageType.Damage);
          }
          break;
        case CombatActionType.Defend:
          caster.SetDefending(true);
          AddCombatMessage($"{caster.Name} is defending.", MessageType.System);
          break;
        case CombatActionType.Item:
          if (action.ItemData != null) {
            InventoryComponent inventory = caster.GetComponent<InventoryComponent>();
            _ = (inventory?.UseItem(action.ItemData, action.Target));
            AddCombatMessage($"{caster.Name} uses {action.ItemData.DisplayName}.", MessageType.Healing);
          }
          break;
        case CombatActionType.Skill:
          if (!string.IsNullOrEmpty(action.SkillId)) {
            SkillComponent skillComponent = caster.GetComponent<SkillComponent>();
            _ = (skillComponent?.UseSkill(action.SkillId, action.Target));
            AddCombatMessage($"{caster.Name} casts {action.SkillId}.", MessageType.System);
          }
          break;
        default:
          break;
      }
    }

    private void ShowItemModal(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      InventoryComponent inventory = combatant.GetComponent<InventoryComponent>();
      if (inventory == null) {
        AddCombatMessage("No items available.", MessageType.System);
        return;
      }

      PopulateItemList(inventory);

      _itemModal?.RemoveFromClassList("is-hidden");
    }

    private void ShowSkillModal(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      SkillComponent skillComponent = combatant.GetComponent<SkillComponent>();
      if (skillComponent == null) {
        AddCombatMessage("No skills available.", MessageType.System);
        return;
      }

      PopulateSkillList(skillComponent);

      _skillModal?.RemoveFromClassList("is-hidden");
    }

    private void HideModals() {
      _itemModal?.AddToClassList("is-hidden");

      _skillModal?.AddToClassList("is-hidden");

      _itemList?.ClearSelection();
      _skillList?.ClearSelection();
    }

    private void PopulateItemList(InventoryComponent inventory) {
      _itemSource.Clear();

      if (inventory != null) {
        _itemSource.AddRange(inventory.GetUsableItems());
      }

      if (_itemList == null) {
        return;
      }
      _itemList.itemsSource = _itemSource;
      _itemList.RefreshItems();
    }

    private void PopulateSkillList(SkillComponent skills) {
      _skillSource.Clear();

      if (skills != null) {
        _skillSource.AddRange(skills.GetAvailableSkills().Select(s => s.Data));
      }

      if (_skillList == null) {
        return;
      }
      _skillList.itemsSource = _skillSource;
      _skillList.RefreshItems();
    }

    private void BindItemEntry(VisualElement element, int index) {
      if (element is Label label && index >= 0 && index < _itemSource.Count) {
        ItemData item = _itemSource[index];
        label.text = item.DisplayName;
        label.tooltip = item.Description;
      }
    }

    private void BindSkillEntry(VisualElement element, int index) {
      if (element is Label label && index >= 0 && index < _skillSource.Count) {
        SkillScriptableObject skill = _skillSource[index];
        label.text = skill.DisplayName;
        label.tooltip = skill.Description;
      }
    }

    private void HandleItemSelectionChanged(IEnumerable<object> selectedItems) {
      var item = selectedItems?.FirstOrDefault() as ItemData;
      if (item == null) {
        return;
      }

      OnItemSelected(item);
    }

    private void HandleSkillSelectionChanged(IEnumerable<object> selectedSkills) {
      var skill = selectedSkills?.FirstOrDefault() as SkillScriptableObject;
      if (skill == null) {
        return;
      }

      OnSkillSelected(skill);
    }

    private void OnItemSelected(ItemData item) {
      _pendingItem = item;
      HideModals();

      if (_currentTurnCombatant == null) {
        return;
      }

      bool requiresTarget = ItemRequiresTarget(item);
      if (requiresTarget) {
        StartTargetSelection(CombatActionType.Item);
      } else {
        ExecuteAction(CombatActionType.Item, _currentTurnCombatant, _currentTurnCombatant, item);
      }
    }

    private void OnSkillSelected(SkillScriptableObject skill) {
      _pendingSkill = skill;
      HideModals();

      if (_currentTurnCombatant == null) {
        return;
      }

      if (RequiresTarget(skill)) {
        StartTargetSelection(CombatActionType.Skill);
      } else {
        ExecuteAction(CombatActionType.Skill, _currentTurnCombatant, _currentTurnCombatant, skill);
      }
    }

    private void AddCombatMessage(string message, MessageType messageType) {
      if (_combatLogContainer == null) {
        return;
      }

      Label entry = _logEntryPool.Count > 0 ? _logEntryPool.Pop() : new Label();
      entry.text = message;
      entry.RemoveFromClassList("log-message-normal");
      entry.RemoveFromClassList("log-message-damage");
      entry.RemoveFromClassList("log-message-healing");
      entry.RemoveFromClassList("log-message-system");

      switch (messageType) {
        case MessageType.Damage:
          entry.AddToClassList("log-message-damage");
          break;
        case MessageType.Healing:
          entry.AddToClassList("log-message-healing");
          break;
        case MessageType.System:
          entry.AddToClassList("log-message-system");
          break;
        case MessageType.Normal:
          break;
        default:
          entry.AddToClassList("log-message-normal");
          break;
      }

      _combatLogContainer.Add(entry);
      _activeLogEntries.Add(entry);

      if (_activeLogEntries.Count > _maxLogEntries) {
        Label oldest = _activeLogEntries[0];
        _activeLogEntries.RemoveAt(0);
        _combatLogContainer.Remove(oldest);
        _logEntryPool.Push(oldest);
      }

      _ = StartCoroutine(UpdateCombatLog());
    }

    private IEnumerator UpdateCombatLog() {
      yield return _logScrollDelay;

      if (_combatLogScrollView != null && _activeLogEntries.Count > 0) {
        _combatLogScrollView.ScrollTo(_activeLogEntries[^1]);
      }
    }

    private List<Combatant> GetValidTargets(CombatActionType actionType, Combatant caster) {
      var targets = new List<Combatant>();
      if (caster == null) {
        return targets;
      }

      switch (actionType) {
        case CombatActionType.Attack:
          targets.AddRange(caster.IsPlayerControlled ? _enemyTeam : _playerTeam);
          break;
        case CombatActionType.Defend:
          targets.Add(caster);
          break;
        case CombatActionType.Item:
          if (_pendingItem != null) {
            targets.AddRange(GetTargetsForItem(_pendingItem, caster));
          } else {
            targets.Add(caster);
          }
          break;
        case CombatActionType.Skill:
          if (_pendingSkill != null) {
            targets.AddRange(GetTargetsForSkill(_pendingSkill, caster));
          } else {
            targets.Add(caster);
          }
          break;
        default:
          break;
      }

      return targets.Where(t => t != null && t.IsAlive).Distinct().ToList();
    }

    private bool IsValidTarget(Combatant target, CombatActionType actionType, Combatant caster) {
      if (target == null) {
        return false;
      }

      List<Combatant> validTargets = GetValidTargets(actionType, caster);
      return validTargets.Contains(target);
    }

    private void ApplyTargetHighlighting() {
      if (!_isSelectingTarget || _currentTurnCombatant == null) {
        return;
      }

      HighlightValidTargets(_currentAction, _currentTurnCombatant);
    }

    private void OnCombatantDamaged(Combatant combatant, int damage) {
      UpdateCombatantDisplay(combatant);
      AddCombatMessage($"{combatant.Name} takes {damage} damage.", MessageType.Damage);
    }

    private void OnCombatantHealed(Combatant combatant, int healing) {
      UpdateCombatantDisplay(combatant);
      AddCombatMessage($"{combatant.Name} recovers {healing} HP.", MessageType.Healing);
    }

    private void OnCombatantDefeated(Combatant combatant) {
      UpdateCombatantDisplay(combatant);
      AddCombatMessage($"{combatant.Name} is defeated!", MessageType.System);
    }

    private void OnCombatantStatChanged(Combatant combatant, StatType statType, int oldValue, int newValue) {
      if (statType == StatType.Health) {
        UpdateCombatantDisplay(combatant);
      }
    }

    private void SetCurrentTurnCombatant(Combatant combatant) {
      _currentTurnCombatant = combatant;
      _combatUIData.SetTurnInfo(combatant);
      UpdateTurnInfoUI();

      foreach (SlotVisualCache cache in _slotCache.Values) {
        if (cache.Root == null) {
          continue;
        }

        if (cache.Combatant == combatant) {
          cache.Root.AddToClassList("current-turn");
        } else {
          cache.Root.RemoveFromClassList("current-turn");
        }
      }

      ApplyTargetHighlighting();
    }

    private void SetupCombatantEventListeners(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      UnsubscribeCombatantEvents(combatant);

      var subscription = new CombatantEventSubscription {
        DamagedHandler = damage => OnCombatantDamaged(combatant, damage),
        HealedHandler = healing => OnCombatantHealed(combatant, healing),
        DefeatedHandler = () => OnCombatantDefeated(combatant),
        StatChangedHandler = (statType, oldValue, newValue) => OnCombatantStatChanged(combatant, statType, oldValue, newValue)
      };

      combatant.OnDamaged += subscription.DamagedHandler;
      combatant.OnHealed += subscription.HealedHandler;
      combatant.OnDefeated += subscription.DefeatedHandler;
      combatant.OnStatChanged += subscription.StatChangedHandler;

      _combatantEventSubscriptions[combatant] = subscription;
    }

    private void UnsubscribeCombatantEvents(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      if (!_combatantEventSubscriptions.TryGetValue(combatant, out CombatantEventSubscription subscription)) {
        return;
      }

      combatant.OnDamaged -= subscription.DamagedHandler;
      combatant.OnHealed -= subscription.HealedHandler;
      combatant.OnDefeated -= subscription.DefeatedHandler;
      combatant.OnStatChanged -= subscription.StatChangedHandler;

      _ = _combatantEventSubscriptions.Remove(combatant);
    }

    private void UnsubscribeFromAllCombatantEvents() {
      foreach (KeyValuePair<Combatant, CombatantEventSubscription> kvp in _combatantEventSubscriptions.ToList()) {
        Combatant combatant = kvp.Key;
        UnsubscribeCombatantEvents(combatant);
      }

      _combatantEventSubscriptions.Clear();
    }

    private void UpdateTurnInfoUI() {
      if (_turnCounterLabel != null) {
        _turnCounterLabel.text = _combatUIData.TurnNumber.ToString();
      }

      if (_battleTimerLabel != null) {
        _battleTimerLabel.text = _combatUIData.BattleTimer;
      }

      if (_currentActionLabel != null) {
        _currentActionLabel.text = _combatUIData.CurrentActionText;
      }

      if (_currentTurnLabel != null) {
        _currentTurnLabel.text = _combatUIData.CurrentTurnCharacter;
      }

      _attackBtn?.SetEnabled(_currentTurnCombatant != null && _currentTurnCombatant.IsAlive);

      _defendBtn?.SetEnabled(_currentTurnCombatant != null && _currentTurnCombatant.IsAlive);

      if (_itemBtn != null) {
        bool hasInventory = _currentTurnCombatant != null && _currentTurnCombatant.GetComponent<InventoryComponent>() != null;
        _itemBtn.SetEnabled(hasInventory);
      }

      if (_skillBtn != null) {
        bool hasSkills = _currentTurnCombatant != null && _currentTurnCombatant.GetComponent<SkillComponent>() != null;
        _skillBtn.SetEnabled(hasSkills);
      }
    }

    private void ResetViewState() {
      EndTargetSelection();
      HideModals();
      _battleTimerSeconds = 0f;
      _combatUIData.Reset();
      UpdateTurnInfoUI();
      ClearTeamGrid(_playerSlots);
      ClearTeamGrid(_enemySlots);
      _playerTeam.Clear();
      _enemyTeam.Clear();
      _combatantSlots.Clear();
      _combatantGridPositions.Clear();
      _combatantUIData.Clear();
      _combatantTemplates.Clear();
      UnsubscribeFromAllCombatantEvents();
    }

    private void ClearTeamGrid(List<VisualElement> slots) {
      foreach (VisualElement slot in slots) {
        ClearCombatantSlot(slot);
      }
    }

    private VisualElement GetSlotAtPosition(List<VisualElement> slots, Vector2Int gridPosition) {
      int index = (gridPosition.y * 3) + gridPosition.x;
      return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    private void StartBattleTimer() {
      StopBattleTimer();
      _battleTimerRoutine = StartCoroutine(BattleTimerTick());
    }

    private void StopBattleTimer() {
      if (_battleTimerRoutine != null) {
        StopCoroutine(_battleTimerRoutine);
        _battleTimerRoutine = null;
      }
    }

    private IEnumerator BattleTimerTick() {
      var wait = new WaitForSeconds(1f);
      while (true) {
        yield return wait;
        _battleTimerSeconds += 1f;
        _combatUIData.SetBattleTimer(TimeSpan.FromSeconds(_battleTimerSeconds).ToString(@"mm\:ss"));
        UpdateTurnInfoUI();
      }
    }

    private bool ItemRequiresTarget(ItemData item) {
      return item != null && item.Effects.Any(effect => !effect.TargetSelf);
    }

    private bool RequiresTarget(SkillScriptableObject skill) {
      return skill != null && skill.TargetType switch {
        TargetType.Self => false,
        TargetType.All => false,
        TargetType.AllAllies => false,
        TargetType.Multiple => true,
        TargetType.AllEnemies => true,
        TargetType.Single => throw new NotImplementedException(),
        _ => skill.CanTargetEnemies || skill.CanTargetAllies
      };
    }

    private IEnumerable<Combatant> GetTargetsForItem(ItemData item, Combatant caster) {
      bool requiresTarget = ItemRequiresTarget(item);
      if (!requiresTarget) {
        return new List<Combatant> { caster };
      }

      var effects = item.Effects.Where(effect => !effect.TargetSelf).ToList();
      if (effects.Count == 0) {
        return new List<Combatant> { caster };
      }

      bool hasDamage = effects.Any(effect => effect.EffectType == EffectType.Damage);
      bool hasHeal = effects.Any(effect => effect.EffectType == EffectType.Heal);

      if (hasDamage && hasHeal) {
        return _playerTeam.Concat(_enemyTeam).Where(combatant => combatant.IsAlive).ToList();
      }

      return hasDamage
        ? caster.IsPlayerControlled ? _enemyTeam : _playerTeam
        : hasHeal ? caster.IsPlayerControlled ? _playerTeam : _enemyTeam : new List<Combatant> { caster };
    }

    private IEnumerable<Combatant> GetTargetsForSkill(SkillScriptableObject skill, Combatant caster) {
      return skill == null
        ? new List<Combatant> { caster }
        : skill.TargetType switch {
          TargetType.Self => new List<Combatant> { caster },
          TargetType.AllAllies => caster.IsPlayerControlled ? _playerTeam : _enemyTeam,
          TargetType.AllEnemies => caster.IsPlayerControlled ? _enemyTeam : _playerTeam,
          TargetType.All => _playerTeam.Concat(_enemyTeam).Where(c => c.IsAlive).ToList(),
          TargetType.Multiple => caster.IsPlayerControlled ? _enemyTeam : _playerTeam,
          TargetType.Single => throw new NotImplementedException(),
          _ => BuildSkillTargetsByAffinity(skill, caster)
        };
    }

    private IEnumerable<Combatant> BuildSkillTargetsByAffinity(SkillScriptableObject skill, Combatant caster) {
      var results = new List<Combatant>();

      if (skill.CanTargetSelf) {
        results.Add(caster);
      }

      if (skill.CanTargetAllies) {
        results.AddRange(caster.IsPlayerControlled ? _playerTeam : _enemyTeam);
      }

      if (skill.CanTargetEnemies) {
        results.AddRange(caster.IsPlayerControlled ? _enemyTeam : _playerTeam);
      }

      return results;
    }

    private void ConfigureListView(ListView listView, IList source, Action<VisualElement, int> bindAction, Action<IEnumerable<object>> selectionHandler) {
      if (listView == null) {
        return;
      }

      listView.makeItem = () => new Label { pickingMode = PickingMode.Position };
      listView.bindItem = bindAction;
      listView.selectionChanged += selectionHandler;
    }

    public void RegisterCombatantTemplate(Combatant combatant, CombatantTemplateScriptableObject template) {
      if (combatant == null || template == null) {
        return;
      }

      _combatantTemplates[combatant] = template;
      UpdateCombatantDisplay(combatant);
    }

    public void GenerateTestCombatants(List<CombatantTemplateScriptableObject> playerTemplates, List<CombatantTemplateScriptableObject> enemyTemplates) {
      var players = new List<Combatant>();
      var enemies = new List<Combatant>();

      if (playerTemplates != null) {
        foreach (CombatantTemplateScriptableObject template in playerTemplates) {
          players.Add(CreateTestCombatantFromTemplate(template, true));
        }
      }

      if (enemyTemplates != null) {
        foreach (CombatantTemplateScriptableObject template in enemyTemplates) {
          enemies.Add(CreateTestCombatantFromTemplate(template, false));
        }
      }

      InitializeBattle(players, enemies);
    }

    public Combatant CreateTestCombatantFromTemplate(CombatantTemplateScriptableObject template, bool isPlayerTeam) {
      if (template == null) {
        return null;
      }

      var go = new GameObject($"Test_{template.displayName}");
      Combatant combatant = go.AddComponent<Combatant>();
      combatant.InitializeFromTemplate(template);
      combatant.SetTeam(isPlayerTeam ? CombatTeam.Player : CombatTeam.Enemy);
      RegisterCombatantTemplate(combatant, template);

      return combatant;
    }

    public void SimulateCombatTurn() {
      if (_currentTurnCombatant == null) {
        if (_playerTeam.Count > 0) {
          SetCurrentTurnCombatant(_playerTeam[0]);
        } else if (_enemyTeam.Count > 0) {
          SetCurrentTurnCombatant(_enemyTeam[0]);
        }
      }

      List<Combatant> targets = GetValidTargets(_currentAction, _currentTurnCombatant);
      if (targets.Count == 0) {
        return;
      }

      Combatant target = targets[UnityEngine.Random.Range(0, targets.Count)];
      ExecuteAction(_currentAction, _currentTurnCombatant, target);
    }

    private void HandleCombatTurnStart(ICombatant combatant) {
      if (combatant is Combatant concrete) {
        SetCurrentTurnCombatant(concrete);
      }

      _combatUIData.SetActionText(string.Empty);
      UpdateTurnInfoUI();
    }

    private void HandleCombatTurnEnd(ICombatant combatant) {
      _combatUIData.IncrementTurn();
      UpdateTurnInfoUI();
    }

    private void HandleActionExecuted(ICombatant combatant, ActionResult result) {
      if (result == null) {
        return;
      }

      AddCombatMessage(result.Message, result.IsSuccess ? MessageType.System : MessageType.System);
    }

    private class SlotVisualCache {
      public VisualElement Root;
      public Vector2Int GridPosition;
      public bool IsPlayerSlot;
      public Label NameLabel;
      public ProgressBar HealthBar;
      public VisualElement Portrait;
      public Combatant Combatant;
    }

    private class CombatantEventSubscription {
      public Action<int> DamagedHandler;
      public Action<int> HealedHandler;
      public Action DefeatedHandler;
      public Action<StatType, int, int> StatChangedHandler;
    }
  }
}


