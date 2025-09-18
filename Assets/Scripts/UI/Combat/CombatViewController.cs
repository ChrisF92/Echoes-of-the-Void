using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Inventory.Data;
using EchoesOfTheVoid.Core.Inventory.ScriptableObjects;
using ItemData = EchoesOfTheVoid.Core.Inventory.ScriptableObjects.ItemScriptableObject;

namespace EchoesOfTheVoid.UI.Combat
{
  public enum MessageType
  {
    Normal,
    Damage,
    Healing,
    System
  }

  [Serializable]
  public class CombatantUIData
  {
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

    public CombatantUIData(Combatant combatant, Vector2Int gridPos)
    {
      GridPosition = gridPos;
      UpdateFromCombatant(combatant);
    }

    public void UpdateFromCombatant(Combatant combatant, Vector2Int? gridPosOverride = null, Sprite portraitOverride = null)
    {
      SourceCombatant = combatant;

      if (combatant == null)
      {
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

      if (gridPosOverride.HasValue)
      {
        GridPosition = gridPosOverride.Value;
      }

      if (portraitOverride != null)
      {
        Portrait = portraitOverride;
      }
    }

    public void SetPortrait(Sprite portrait)
    {
      Portrait = portrait;
    }

    public void SetDefendingState(bool defending)
    {
      IsDefending = defending;
      IsDefendingThisTurn = defending;
    }
  }

  [Serializable]
  public class CombatUIData : INotifyValueChanged<int>
  {
    [CreateProperty] public int TurnNumber { get; private set; }
    [CreateProperty] public string BattleTimer { get; private set; } = "00:00";
    [CreateProperty] public string CurrentActionText { get; private set; } = string.Empty;
    [CreateProperty] public string CurrentTurnCharacter { get; private set; } = string.Empty;
    [CreateProperty] public bool IsPlayerTurn { get; private set; }

    public event Action<int> valueChanged;

    public void Reset()
    {
      TurnNumber = 1;
      BattleTimer = "00:00";
      CurrentActionText = string.Empty;
      CurrentTurnCharacter = string.Empty;
      IsPlayerTurn = false;
      valueChanged?.Invoke(TurnNumber);
    }

    public void IncrementTurn()
    {
      TurnNumber++;
      valueChanged?.Invoke(TurnNumber);
    }

    public void SetTurnInfo(Combatant combatant)
    {
      CurrentTurnCharacter = combatant != null ? combatant.Name : string.Empty;
      IsPlayerTurn = combatant != null && combatant.IsPlayerControlled;
    }

    public void SetActionText(string actionText)
    {
      CurrentActionText = actionText;
    }

    public void SetBattleTimer(string timerText)
    {
      BattleTimer = timerText;
    }

    public int value
    {
      get => TurnNumber;
      set
      {
        TurnNumber = value;
        valueChanged?.Invoke(TurnNumber);
      }
    }

    public void SetTurnNumber(int turnNumber)
    {
      TurnNumber = Mathf.Max(1, turnNumber);
      valueChanged?.Invoke(TurnNumber);
    }

    public void SetValueWithoutNotify(int newValue) {
      throw new NotImplementedException();
    }
  }

  [DisallowMultipleComponent]
  public class CombatViewController : MonoBehaviour
  {
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private CombatUIData combatUIData = new CombatUIData();
    [SerializeField] private CombatSystem combatSystem;

    private VisualElement rootElement;
    private VisualElement playerGrid;
    private VisualElement enemyGrid;

    private readonly List<VisualElement> playerSlots = new List<VisualElement>(9);
    private readonly List<VisualElement> enemySlots = new List<VisualElement>(9);

    private readonly List<Combatant> playerTeam = new List<Combatant>();
    private readonly List<Combatant> enemyTeam = new List<Combatant>();

    private readonly Dictionary<Combatant, CombatantUIData> combatantUIData = new Dictionary<Combatant, CombatantUIData>();
    private readonly Dictionary<Combatant, VisualElement> combatantSlots = new Dictionary<Combatant, VisualElement>();
    private readonly Dictionary<Combatant, Vector2Int> combatantGridPositions = new Dictionary<Combatant, Vector2Int>();
    private readonly Dictionary<VisualElement, SlotVisualCache> slotCache = new Dictionary<VisualElement, SlotVisualCache>();
    private readonly Dictionary<Combatant, CombatantEventSubscription> combatantEventSubscriptions = new Dictionary<Combatant, CombatantEventSubscription>();
    private readonly Dictionary<Combatant, CombatantTemplateScriptableObject> combatantTemplates = new Dictionary<Combatant, CombatantTemplateScriptableObject>();

    private bool isSelectingTarget;
    private CombatActionType currentAction = CombatActionType.Attack;
    private readonly List<VisualElement> validTargets = new List<VisualElement>();
    private Combatant selectedCombatant;
    private Combatant currentTurnCombatant;

    private VisualElement itemModal;
    private VisualElement skillModal;
    private ListView itemList;
    private ListView skillList;

    private Button attackBtn;
    private Button defendBtn;
    private Button itemBtn;
    private Button skillBtn;
    private Button itemModalCloseBtn;
    private Button skillModalCloseBtn;

    private Label turnCounterLabel;
    private Label battleTimerLabel;
    private Label currentActionLabel;
    private Label currentTurnLabel;

    private ScrollView combatLogScrollView;
    private VisualElement combatLogContainer;
    private readonly List<Label> activeLogEntries = new List<Label>();
    private readonly Stack<Label> logEntryPool = new Stack<Label>();
    private const int MaxLogEntries = 60;

    private readonly WaitForSeconds logScrollDelay = new WaitForSeconds(0.01f);

    private float battleTimerSeconds;
    private Coroutine battleTimerRoutine;

    private SkillScriptableObject pendingSkill;
    private ItemScriptableObject pendingItem;

    private readonly List<ItemScriptableObject> itemSource = new List<ItemScriptableObject>();
    private readonly List<SkillScriptableObject> skillSource = new List<SkillScriptableObject>();

    private bool isInitialized;

    private void Awake()
    {
      if (uiDocument == null)
      {
        uiDocument = GetComponent<UIDocument>();
      }

      if (uiDocument == null)
      {
        Debug.LogError("CombatViewController requires a UIDocument reference.", this);
        enabled = false;
        return;
      }

      rootElement = uiDocument.rootVisualElement;
      if (rootElement == null)
      {
        Debug.LogError("CombatViewController failed to locate root visual element.", this);
        enabled = false;
        return;
      }

      if (combatSystem == null)
      {
        combatSystem = CombatSystem.Instance;
      }

      SetupUIReferences();
      InitializeCombatGrids();
      SetupDataBinding();
      isInitialized = true;
    }

    private void Start()
    {
      if (!isInitialized)
      {
        return;
      }

      SetupButtonEvents();
      HideModals();
      UpdateTurnInfoUI();
    }

    private void OnEnable()
    {
      if (combatSystem != null)
      {
        combatSystem.OnTurnStart += HandleCombatTurnStart;
        combatSystem.OnTurnEnd += HandleCombatTurnEnd;
        combatSystem.OnActionExecuted += HandleActionExecuted;
      }
    }

    private void OnDisable()
    {
      if (combatSystem != null)
      {
        combatSystem.OnTurnStart -= HandleCombatTurnStart;
        combatSystem.OnTurnEnd -= HandleCombatTurnEnd;
        combatSystem.OnActionExecuted -= HandleActionExecuted;
      }
    }

    private void OnDestroy()
    {
      StopBattleTimer();
      UnsubscribeFromAllCombatantEvents();
    }

    public void InitializeBattle(List<Combatant> players, List<Combatant> enemies)
    {
      if (!isInitialized)
      {
        Debug.LogWarning("CombatViewController not initialized before InitializeBattle call.", this);
        return;
      }

      ResetViewState();
      SetPlayerTeam(players);
      SetEnemyTeam(enemies);
      StartBattleTimer();

      if (combatSystem != null && combatSystem.CurrentTurnCombatant is Combatant combatant)
      {
        SetCurrentTurnCombatant(combatant);
      }
    }

    public void SetActivePlayer(Combatant combatant)
    {
      SetCurrentTurnCombatant(combatant);
    }

    public void HandleCombatAction(CombatActionType actionType, Combatant caster, Combatant target, object actionData = null)
    {
      ExecuteAction(actionType, caster, target, actionData);
    }

    private void SetupUIReferences()
    {
      playerGrid = rootElement.Q<VisualElement>("player-grid");
      enemyGrid = rootElement.Q<VisualElement>("enemy-grid");

      attackBtn = rootElement.Q<Button>("attack-btn");
      defendBtn = rootElement.Q<Button>("defend-btn");
      itemBtn = rootElement.Q<Button>("item-btn");
      skillBtn = rootElement.Q<Button>("skill-btn");
      itemModalCloseBtn = rootElement.Q<Button>("item-close-btn");
      skillModalCloseBtn = rootElement.Q<Button>("skill-close-btn");

      itemModal = rootElement.Q<VisualElement>("item-modal");
      skillModal = rootElement.Q<VisualElement>("skill-modal");
      itemList = rootElement.Q<ListView>("item-list");
      skillList = rootElement.Q<ListView>("skill-list");

      combatLogScrollView = rootElement.Q<ScrollView>("combat-log");
      combatLogContainer = rootElement.Q<VisualElement>("log-messages");

      turnCounterLabel = rootElement.Q<Label>("turn-counter");
      battleTimerLabel = rootElement.Q<Label>("battle-timer");
      currentActionLabel = rootElement.Q<Label>("current-action");
      currentTurnLabel = rootElement.Q<Label>("current-turn-character");

      ConfigureListView(itemList, itemSource, BindItemEntry, HandleItemSelectionChanged);
      ConfigureListView(skillList, skillSource, BindSkillEntry, HandleSkillSelectionChanged);
    }

    private void SetupDataBinding()
    {
      if (combatUIData == null)
      {
        combatUIData = new CombatUIData();
      }

      rootElement.dataSource = combatUIData;
      UpdateTurnInfoUI();
    }

    private void SetupButtonEvents()
    {
      if (attackBtn != null)
      {
        attackBtn.clicked += () => OnActionButtonClicked(CombatActionType.Attack);
      }

      if (defendBtn != null)
      {
        defendBtn.clicked += () => OnActionButtonClicked(CombatActionType.Defend);
      }

      if (itemBtn != null)
      {
        itemBtn.clicked += () => OnActionButtonClicked(CombatActionType.Item);
      }

      if (skillBtn != null)
      {
        skillBtn.clicked += () => OnActionButtonClicked(CombatActionType.Skill);
      }

      if (itemModalCloseBtn != null)
      {
        itemModalCloseBtn.clicked += HideModals;
      }

      if (skillModalCloseBtn != null)
      {
        skillModalCloseBtn.clicked += HideModals;
      }
    }

    private void InitializeCombatGrids()
    {
      playerSlots.Clear();
      enemySlots.Clear();

      if (playerGrid != null)
      {
        for (var index = 0; index < 9; index++)
        {
          var slot = playerGrid.Q<VisualElement>($"player-slot-{index}");
          if (slot != null)
          {
            CacheSlot(slot, new Vector2Int(index % 3, index / 3), true);
            playerSlots.Add(slot);
          }
        }
      }

      if (enemyGrid != null)
      {
        for (var index = 0; index < 9; index++)
        {
          var slot = enemyGrid.Q<VisualElement>($"enemy-slot-{index}");
          if (slot != null)
          {
            CacheSlot(slot, new Vector2Int(index % 3, index / 3), false);
            enemySlots.Add(slot);
          }
        }
      }
    }

    private void CacheSlot(VisualElement slot, Vector2Int gridPos, bool isPlayerTeam)
    {
      if (slotCache.ContainsKey(slot))
      {
        return;
      }

      var cache = new SlotVisualCache
      {
        Root = slot,
        GridPosition = gridPos,
        IsPlayerSlot = isPlayerTeam,
        NameLabel = slot.Q<Label>(className: "combatant-name"),
        HealthBar = slot.Q<ProgressBar>(className: "health-bar"),
        Portrait = slot.Q<VisualElement>(className: "portrait")
      };

      slotCache.Add(slot, cache);

      slot.RegisterCallback<ClickEvent>(_ =>
      {
        OnCombatantSlotClicked(slot, cache.Combatant);
      });
    }

    private void SetPlayerTeam(List<Combatant> combatants)
    {
      ClearTeamGrid(playerSlots);
      playerTeam.Clear();

      if (combatants == null)
      {
        return;
      }

      for (var i = 0; i < combatants.Count && i < playerSlots.Count; i++)
      {
        var combatant = combatants[i];
        var slot = playerSlots[i];
        var gridPos = slotCache.TryGetValue(slot, out var cache) ? cache.GridPosition : new Vector2Int(i % 3, i / 3);
        AddCombatantToGridInternal(combatant, slot, gridPos);
        playerTeam.Add(combatant);
      }
    }

    private void SetEnemyTeam(List<Combatant> combatants)
    {
      ClearTeamGrid(enemySlots);
      enemyTeam.Clear();

      if (combatants == null)
      {
        return;
      }

      for (var i = 0; i < combatants.Count && i < enemySlots.Count; i++)
      {
        var combatant = combatants[i];
        var slot = enemySlots[i];
        var gridPos = slotCache.TryGetValue(slot, out var cache) ? cache.GridPosition : new Vector2Int(i % 3, i / 3);
        AddCombatantToGridInternal(combatant, slot, gridPos);
        enemyTeam.Add(combatant);
      }
    }

    public void AddCombatantToGrid(Combatant combatant, Vector2Int gridPosition)
    {
      if (combatant == null)
      {
        Debug.LogWarning("Attempted to add a null combatant to the grid.", this);
        return;
      }

      var slotList = combatant.IsPlayerControlled ? playerSlots : enemySlots;
      var slot = GetSlotAtPosition(slotList, gridPosition);

      if (slot == null)
      {
        Debug.LogWarning($"Invalid grid position {gridPosition} for combatant {combatant.name}.", this);
        return;
      }

      AddCombatantToGridInternal(combatant, slot, gridPosition);

      if (combatant.IsPlayerControlled && !playerTeam.Contains(combatant))
      {
        playerTeam.Add(combatant);
      }
      else if (!combatant.IsPlayerControlled && !enemyTeam.Contains(combatant))
      {
        enemyTeam.Add(combatant);
      }
    }

    private void AddCombatantToGridInternal(Combatant combatant, VisualElement slot, Vector2Int gridPosition)
    {
      if (combatant == null || slot == null)
      {
        return;
      }

      combatantGridPositions[combatant] = gridPosition;
      combatantSlots[combatant] = slot;

      if (!combatantUIData.TryGetValue(combatant, out var uiData))
      {
        uiData = new CombatantUIData(combatant, gridPosition);
        combatantUIData[combatant] = uiData;
      }
      else
      {
        uiData.UpdateFromCombatant(combatant, gridPosition);
      }

      if (combatantTemplates.TryGetValue(combatant, out var template) && template != null)
      {
        uiData.SetPortrait(template.portrait);
      }

      PopulateCombatantSlot(slot, combatant, gridPosition);
      SetupCombatantEventListeners(combatant);
    }

    public void RemoveCombatantFromGrid(Combatant combatant)
    {
      if (combatant == null)
      {
        return;
      }

      if (combatantSlots.TryGetValue(combatant, out var slot))
      {
        ClearCombatantSlot(slot);
      }

      combatantSlots.Remove(combatant);
      combatantGridPositions.Remove(combatant);
      combatantUIData.Remove(combatant);
      playerTeam.Remove(combatant);
      enemyTeam.Remove(combatant);

      UnsubscribeCombatantEvents(combatant);
    }

    public Vector2Int GetGridPosition(Combatant combatant)
    {
      return combatantGridPositions.TryGetValue(combatant, out var position) ? position : new Vector2Int(-1, -1);
    }

    private void PopulateCombatantSlot(VisualElement slot, Combatant combatant, Vector2Int gridPos)
    {
      if (slot == null)
      {
        return;
      }

      if (!slotCache.TryGetValue(slot, out var cache))
      {
        return;
      }

      cache.Combatant = combatant;

      slot.RemoveFromClassList("is-empty");
      slot.AddToClassList("combatant-alive");
      slot.RemoveFromClassList("combatant-dead");

      if (combatantUIData.TryGetValue(combatant, out var uiData))
      {
        ApplyCombatantDataToSlot(cache, uiData);
      }
    }

    private void UpdateCombatantDisplay(Combatant combatant)
    {
      if (combatant == null)
      {
        return;
      }

      if (!combatantUIData.TryGetValue(combatant, out var uiData))
      {
        uiData = new CombatantUIData(combatant, GetGridPosition(combatant));
        combatantUIData[combatant] = uiData;
      }
      else
      {
        var portrait = combatantTemplates.TryGetValue(combatant, out var template) ? template.portrait : uiData.Portrait;
        uiData.UpdateFromCombatant(combatant, GetGridPosition(combatant), portrait);
      }

      if (!combatantSlots.TryGetValue(combatant, out var slot))
      {
        return;
      }

      if (!slotCache.TryGetValue(slot, out var cache))
      {
        return;
      }

      ApplyCombatantDataToSlot(cache, uiData);
      ApplyTargetHighlighting();
    }

    private void ApplyCombatantDataToSlot(SlotVisualCache cache, CombatantUIData uiData)
    {
      if (cache.NameLabel != null)
      {
        cache.NameLabel.bindingPath = nameof(CombatantUIData.Name);
        cache.NameLabel.dataSource = uiData;
        cache.NameLabel.text = uiData.Name;
      }

      if (cache.HealthBar != null)
      {
        cache.HealthBar.bindingPath = nameof(CombatantUIData.HPPercentage);
        cache.HealthBar.dataSource = uiData;
        cache.HealthBar.lowValue = 0f;
        cache.HealthBar.highValue = 1f;
        cache.HealthBar.value = uiData.HPPercentage;
        cache.HealthBar.title = $"{uiData.CurrentHP}/{uiData.MaxHP}";
      }

      if (cache.Portrait != null)
      {
        if (uiData.Portrait != null)
        {
          cache.Portrait.style.backgroundImage = new StyleBackground(uiData.Portrait);
          cache.Portrait.RemoveFromClassList("portrait-empty");
        }
        else
        {
          cache.Portrait.style.backgroundImage = StyleKeyword.Null;
          cache.Portrait.AddToClassList("portrait-empty");
        }
      }

      if (cache.Root != null)
      {
        if (uiData.IsAlive)
        {
          cache.Root.AddToClassList("combatant-alive");
          cache.Root.RemoveFromClassList("combatant-dead");
        }
        else
        {
          cache.Root.AddToClassList("combatant-dead");
          cache.Root.RemoveFromClassList("combatant-alive");
        }

        if (uiData.IsDefending)
        {
          cache.Root.AddToClassList("combatant-defending");
        }
        else
        {
          cache.Root.RemoveFromClassList("combatant-defending");
        }
      }
    }

    private void ClearCombatantSlot(VisualElement slot)
    {
      if (slot == null || !slotCache.TryGetValue(slot, out var cache))
      {
        return;
      }

      cache.Combatant = null;

      slot.AddToClassList("is-empty");
      slot.RemoveFromClassList("combatant-alive");
      slot.RemoveFromClassList("combatant-dead");
      slot.RemoveFromClassList("valid-target");
      slot.RemoveFromClassList("invalid-target");
      slot.RemoveFromClassList("selected-target");

      if (cache.NameLabel != null)
      {
        cache.NameLabel.text = "-";
      }

      if (cache.HealthBar != null)
      {
        cache.HealthBar.value = 0f;
        cache.HealthBar.title = string.Empty;
      }

      if (cache.Portrait != null)
      {
        cache.Portrait.style.backgroundImage = StyleKeyword.Null;
        cache.Portrait.AddToClassList("portrait-empty");
      }
    }

    private void OnActionButtonClicked(CombatActionType actionType)
    {
      if (currentTurnCombatant == null)
      {
        AddCombatMessage("No active combatant.", MessageType.System);
        return;
      }

      currentAction = actionType;
      combatUIData.SetActionText(actionType.ToString());
      UpdateTurnInfoUI();

      switch (actionType)
      {
        case CombatActionType.Attack:
          StartTargetSelection(actionType);
          break;
        case CombatActionType.Defend:
          ExecuteAction(CombatActionType.Defend, currentTurnCombatant, currentTurnCombatant);
          break;
        case CombatActionType.Item:
          ShowItemModal(currentTurnCombatant);
          break;
        case CombatActionType.Skill:
          ShowSkillModal(currentTurnCombatant);
          break;
      }
    }

    private void StartTargetSelection(CombatActionType actionType)
    {
      if (currentTurnCombatant == null)
      {
        return;
      }

      isSelectingTarget = true;
      HighlightValidTargets(actionType, currentTurnCombatant);
    }

    private void EndTargetSelection()
    {
      isSelectingTarget = false;
      selectedCombatant = null;
      validTargets.Clear();

      foreach (var cache in slotCache.Values)
      {
        cache.Root.RemoveFromClassList("valid-target");
        cache.Root.RemoveFromClassList("invalid-target");
        cache.Root.RemoveFromClassList("selected-target");
      }
    }

    private void HighlightValidTargets(CombatActionType actionType, Combatant caster)
    {
      if (caster == null)
      {
        return;
      }

      var validCombatants = GetValidTargets(actionType, caster);
      validTargets.Clear();

      foreach (var cache in slotCache.Values)
      {
        cache.Root.RemoveFromClassList("valid-target");
        cache.Root.RemoveFromClassList("invalid-target");
        cache.Root.RemoveFromClassList("selected-target");

        if (cache.Combatant == null || !cache.Combatant.IsAlive)
        {
          continue;
        }

        if (validCombatants.Contains(cache.Combatant))
        {
          cache.Root.AddToClassList("valid-target");
          validTargets.Add(cache.Root);
        }
        else
        {
          cache.Root.AddToClassList("invalid-target");
        }
      }
    }

    private void OnCombatantSlotClicked(VisualElement slot, Combatant combatant)
    {
      if (!isSelectingTarget)
      {
        return;
      }

      if (combatant == null)
      {
        AddCombatMessage("Cannot target an empty slot.", MessageType.System);
        return;
      }

      if (!IsValidTarget(combatant, currentAction, currentTurnCombatant))
      {
        AddCombatMessage($"{combatant.Name} is not a valid target.", MessageType.System);
        return;
      }

      selectedCombatant = combatant;

      foreach (var cache in slotCache.Values)
      {
        cache.Root.RemoveFromClassList("selected-target");
      }

      if (slot != null)
      {
        slot.AddToClassList("selected-target");
      }

      ExecuteAction(currentAction, currentTurnCombatant, combatant, pendingSkill != null ? pendingSkill : (object)pendingItem);
      EndTargetSelection();
    }

    public void ExecuteAction(CombatActionType actionType, Combatant caster, Combatant target, object actionData = null)
    {
      if (caster == null)
      {
        Debug.LogWarning("ExecuteAction called without a caster.", this);
        return;
      }

      var combatAction = new CombatAction
      {
        ActionType = actionType,
        Target = target
      };

      switch (actionType)
      {
        case CombatActionType.Attack:
          if (target == null)
          {
            AddCombatMessage("Attack requires a target.", MessageType.System);
            return;
          }
          break;
        case CombatActionType.Defend:
          combatAction.Target = caster;
          caster.SetDefending(true);
          break;
        case CombatActionType.Item:
          var item = actionData as ItemScriptableObject ?? pendingItem;
          if (item == null)
          {
            AddCombatMessage("Select an item first.", MessageType.System);
            return;
          }
          combatAction.ItemData = item;
          if (target == null)
          {
            combatAction.Target = caster;
          }
          break;
        case CombatActionType.Skill:
          var skill = actionData as SkillScriptableObject ?? pendingSkill;
          if (skill == null)
          {
            AddCombatMessage("Select a skill first.", MessageType.System);
            return;
          }
          combatAction.SkillId = skill.skillId;
          if (target == null && RequiresTarget(skill))
          {
            AddCombatMessage("Skill requires a target.", MessageType.System);
            return;
          }
          if (!RequiresTarget(skill))
          {
            combatAction.Target = caster;
          }
          break;
      }

      combatUIData.SetActionText(actionType.ToString());
      UpdateTurnInfoUI();

      if (combatSystem != null)
      {
        var success = combatSystem.ExecuteAction(caster, combatAction);
        if (!success)
        {
          AddCombatMessage("Action could not be executed.", MessageType.System);
        }
      }
      else
      {
        ApplyLocalActionSimulation(caster, combatAction);
      }

      pendingItem = null;
      pendingSkill = null;
      HideModals();
    }

    private void ApplyLocalActionSimulation(Combatant caster, CombatAction action)
    {
      switch (action.ActionType)
      {
        case CombatActionType.Attack:
          if (action.Target is Combatant targetCombatant)
          {
            targetCombatant.TakeDamage(Mathf.Max(1, caster.GetStat(StatType.Attack) - targetCombatant.GetStat(StatType.Defense)));
            AddCombatMessage($"{caster.Name} attacks {targetCombatant.Name}.", MessageType.Damage);
          }
          break;
        case CombatActionType.Defend:
          caster.SetDefending(true);
          AddCombatMessage($"{caster.Name} is defending.", MessageType.System);
          break;
        case CombatActionType.Item:
          if (action.ItemData != null)
          {
            var inventory = caster.GetComponent<InventoryComponent>();
            inventory?.UseItem(action.ItemData, action.Target);
            AddCombatMessage($"{caster.Name} uses {action.ItemData.displayName}.", MessageType.Healing);
          }
          break;
        case CombatActionType.Skill:
          if (!string.IsNullOrEmpty(action.SkillId))
          {
            var skillComponent = caster.GetComponent<SkillComponent>();
            skillComponent?.UseSkill(action.SkillId, action.Target);
            AddCombatMessage($"{caster.Name} casts {action.SkillId}.", MessageType.System);
          }
          break;
      }
    }

    private void ShowItemModal(Combatant combatant)
    {
      if (combatant == null)
      {
        return;
      }

      var inventory = combatant.GetComponent<InventoryComponent>();
      if (inventory == null)
      {
        AddCombatMessage("No items available.", MessageType.System);
        return;
      }

      PopulateItemList(inventory);

      if (itemModal != null)
      {
        itemModal.RemoveFromClassList("is-hidden");
      }
    }

    private void ShowSkillModal(Combatant combatant)
    {
      if (combatant == null)
      {
        return;
      }

      var skillComponent = combatant.GetComponent<SkillComponent>();
      if (skillComponent == null)
      {
        AddCombatMessage("No skills available.", MessageType.System);
        return;
      }

      PopulateSkillList(skillComponent);

      if (skillModal != null)
      {
        skillModal.RemoveFromClassList("is-hidden");
      }
    }

    private void HideModals()
    {
      if (itemModal != null)
      {
        itemModal.AddToClassList("is-hidden");
      }

      if (skillModal != null)
      {
        skillModal.AddToClassList("is-hidden");
      }

      itemList?.ClearSelection();
      skillList?.ClearSelection();
    }

    private void PopulateItemList(InventoryComponent inventory)
    {
      itemSource.Clear();

      if (inventory != null)
      {
        itemSource.AddRange(inventory.GetUsableItems());
      }

      if (itemList == null)
      {
        return;
      }
      itemList.itemsSource = itemSource;
      itemList.RefreshItems();
    }

    private void PopulateSkillList(SkillComponent skills)
    {
      skillSource.Clear();

      if (skills != null)
      {
        skillSource.AddRange(skills.GetAvailableSkills().Select(s => s.Data));
      }

      if (skillList == null)
      {
        return;
      }
      skillList.itemsSource = skillSource;
      skillList.RefreshItems();
    }

    private void BindItemEntry(VisualElement element, int index)
    {
      if (element is Label label && index >= 0 && index < itemSource.Count)
      {
        var item = itemSource[index];
        label.text = item.displayName;
        label.tooltip = item.description;
      }
    }

    private void BindSkillEntry(VisualElement element, int index)
    {
      if (element is Label label && index >= 0 && index < skillSource.Count)
      {
        var skill = skillSource[index];
        label.text = skill.displayName;
        label.tooltip = skill.description;
      }
    }

    private void HandleItemSelectionChanged(IEnumerable<object> selectedItems)
    {
      var item = selectedItems?.FirstOrDefault() as ItemScriptableObject;
      if (item == null)
      {
        return;
      }

      OnItemSelected(item);
    }

    private void HandleSkillSelectionChanged(IEnumerable<object> selectedSkills)
    {
      var skill = selectedSkills?.FirstOrDefault() as SkillScriptableObject;
      if (skill == null)
      {
        return;
      }

      OnSkillSelected(skill);
    }

    private void OnItemSelected(ItemData item)
    {
      pendingItem = item;
      HideModals();

      if (currentTurnCombatant == null)
      {
        return;
      }

      var requiresTarget = ItemRequiresTarget(item);
      if (requiresTarget)
      {
        StartTargetSelection(CombatActionType.Item);
      }
      else
      {
        ExecuteAction(CombatActionType.Item, currentTurnCombatant, currentTurnCombatant, item);
      }
    }

    private void OnSkillSelected(SkillScriptableObject skill)
    {
      pendingSkill = skill;
      HideModals();

      if (currentTurnCombatant == null)
      {
        return;
      }

      if (RequiresTarget(skill))
      {
        StartTargetSelection(CombatActionType.Skill);
      }
      else
      {
        ExecuteAction(CombatActionType.Skill, currentTurnCombatant, currentTurnCombatant, skill);
      }
    }

    private void AddCombatMessage(string message, MessageType messageType)
    {
      if (combatLogContainer == null)
      {
        return;
      }

      var entry = logEntryPool.Count > 0 ? logEntryPool.Pop() : new Label();
      entry.text = message;
      entry.RemoveFromClassList("log-message-normal");
      entry.RemoveFromClassList("log-message-damage");
      entry.RemoveFromClassList("log-message-healing");
      entry.RemoveFromClassList("log-message-system");

      switch (messageType)
      {
        case MessageType.Damage:
          entry.AddToClassList("log-message-damage");
          break;
        case MessageType.Healing:
          entry.AddToClassList("log-message-healing");
          break;
        case MessageType.System:
          entry.AddToClassList("log-message-system");
          break;
        default:
          entry.AddToClassList("log-message-normal");
          break;
      }

      combatLogContainer.Add(entry);
      activeLogEntries.Add(entry);

      if (activeLogEntries.Count > MaxLogEntries)
      {
        var oldest = activeLogEntries[0];
        activeLogEntries.RemoveAt(0);
        combatLogContainer.Remove(oldest);
        logEntryPool.Push(oldest);
      }

      StartCoroutine(UpdateCombatLog());
    }

    private IEnumerator UpdateCombatLog()
    {
      yield return logScrollDelay;

      if (combatLogScrollView != null && activeLogEntries.Count > 0)
      {
        combatLogScrollView.ScrollTo(activeLogEntries[^1]);
      }
    }

    private List<Combatant> GetValidTargets(CombatActionType actionType, Combatant caster)
    {
      var targets = new List<Combatant>();
      if (caster == null)
      {
        return targets;
      }

      switch (actionType)
      {
        case CombatActionType.Attack:
          targets.AddRange(caster.IsPlayerControlled ? enemyTeam : playerTeam);
          break;
        case CombatActionType.Defend:
          targets.Add(caster);
          break;
        case CombatActionType.Item:
          if (pendingItem != null)
          {
            targets.AddRange(GetTargetsForItem(pendingItem, caster));
          }
          else
          {
            targets.Add(caster);
          }
          break;
        case CombatActionType.Skill:
          if (pendingSkill != null)
          {
            targets.AddRange(GetTargetsForSkill(pendingSkill, caster));
          }
          else
          {
            targets.Add(caster);
          }
          break;
      }

      return targets.Where(t => t != null && t.IsAlive).Distinct().ToList();
    }

    private bool IsValidTarget(Combatant target, CombatActionType actionType, Combatant caster)
    {
      if (target == null)
      {
        return false;
      }

      var validTargets = GetValidTargets(actionType, caster);
      return validTargets.Contains(target);
    }

    private void ApplyTargetHighlighting()
    {
      if (!isSelectingTarget || currentTurnCombatant == null)
      {
        return;
      }

      HighlightValidTargets(currentAction, currentTurnCombatant);
    }

    private void OnCombatantDamaged(Combatant combatant, int damage)
    {
      UpdateCombatantDisplay(combatant);
      AddCombatMessage($"{combatant.Name} takes {damage} damage.", MessageType.Damage);
    }

    private void OnCombatantHealed(Combatant combatant, int healing)
    {
      UpdateCombatantDisplay(combatant);
      AddCombatMessage($"{combatant.Name} recovers {healing} HP.", MessageType.Healing);
    }

    private void OnCombatantDefeated(Combatant combatant)
    {
      UpdateCombatantDisplay(combatant);
      AddCombatMessage($"{combatant.Name} is defeated!", MessageType.System);
    }

    private void OnCombatantStatChanged(Combatant combatant, StatType statType, int oldValue, int newValue)
    {
      if (statType == StatType.Health)
      {
        UpdateCombatantDisplay(combatant);
      }
    }

    private void SetCurrentTurnCombatant(Combatant combatant)
    {
      currentTurnCombatant = combatant;
      combatUIData.SetTurnInfo(combatant);
      UpdateTurnInfoUI();

      foreach (var cache in slotCache.Values)
      {
        if (cache.Root == null)
        {
          continue;
        }

        if (cache.Combatant == combatant)
        {
          cache.Root.AddToClassList("current-turn");
        }
        else
        {
          cache.Root.RemoveFromClassList("current-turn");
        }
      }

      ApplyTargetHighlighting();
    }

    private void SetupCombatantEventListeners(Combatant combatant)
    {
      if (combatant == null)
      {
        return;
      }

      UnsubscribeCombatantEvents(combatant);

      var subscription = new CombatantEventSubscription
      {
        DamagedHandler = damage => OnCombatantDamaged(combatant, damage),
        HealedHandler = healing => OnCombatantHealed(combatant, healing),
        DefeatedHandler = () => OnCombatantDefeated(combatant),
        StatChangedHandler = (statType, oldValue, newValue) => OnCombatantStatChanged(combatant, statType, oldValue, newValue)
      };

      combatant.OnDamaged += subscription.DamagedHandler;
      combatant.OnHealed += subscription.HealedHandler;
      combatant.OnDefeated += subscription.DefeatedHandler;
      combatant.OnStatChanged += subscription.StatChangedHandler;

      combatantEventSubscriptions[combatant] = subscription;
    }

    private void UnsubscribeCombatantEvents(Combatant combatant)
    {
      if (combatant == null)
      {
        return;
      }

      if (!combatantEventSubscriptions.TryGetValue(combatant, out var subscription))
      {
        return;
      }

      combatant.OnDamaged -= subscription.DamagedHandler;
      combatant.OnHealed -= subscription.HealedHandler;
      combatant.OnDefeated -= subscription.DefeatedHandler;
      combatant.OnStatChanged -= subscription.StatChangedHandler;

      combatantEventSubscriptions.Remove(combatant);
    }

    private void UnsubscribeFromAllCombatantEvents()
    {
      foreach (var kvp in combatantEventSubscriptions.ToList())
      {
        var combatant = kvp.Key;
        UnsubscribeCombatantEvents(combatant);
      }

      combatantEventSubscriptions.Clear();
    }

    private void UpdateTurnInfoUI()
    {
      if (turnCounterLabel != null)
      {
        turnCounterLabel.text = combatUIData.TurnNumber.ToString();
      }

      if (battleTimerLabel != null)
      {
        battleTimerLabel.text = combatUIData.BattleTimer;
      }

      if (currentActionLabel != null)
      {
        currentActionLabel.text = combatUIData.CurrentActionText;
      }

      if (currentTurnLabel != null)
      {
        currentTurnLabel.text = combatUIData.CurrentTurnCharacter;
      }

      if (attackBtn != null)
      {
        attackBtn.SetEnabled(currentTurnCombatant != null && currentTurnCombatant.IsAlive);
      }

      if (defendBtn != null)
      {
        defendBtn.SetEnabled(currentTurnCombatant != null && currentTurnCombatant.IsAlive);
      }

      if (itemBtn != null)
      {
        var hasInventory = currentTurnCombatant != null && currentTurnCombatant.GetComponent<InventoryComponent>() != null;
        itemBtn.SetEnabled(hasInventory);
      }

      if (skillBtn != null)
      {
        var hasSkills = currentTurnCombatant != null && currentTurnCombatant.GetComponent<SkillComponent>() != null;
        skillBtn.SetEnabled(hasSkills);
      }
    }

    private void ResetViewState()
    {
      EndTargetSelection();
      HideModals();
      battleTimerSeconds = 0f;
      combatUIData.Reset();
      UpdateTurnInfoUI();
      ClearTeamGrid(playerSlots);
      ClearTeamGrid(enemySlots);
      playerTeam.Clear();
      enemyTeam.Clear();
      combatantSlots.Clear();
      combatantGridPositions.Clear();
      combatantUIData.Clear();
      combatantTemplates.Clear();
      UnsubscribeFromAllCombatantEvents();
    }

    private void ClearTeamGrid(List<VisualElement> slots)
    {
      foreach (var slot in slots)
      {
        ClearCombatantSlot(slot);
      }
    }

    private VisualElement GetSlotAtPosition(List<VisualElement> slots, Vector2Int gridPosition)
    {
      var index = gridPosition.y * 3 + gridPosition.x;
      return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    private void StartBattleTimer()
    {
      StopBattleTimer();
      battleTimerRoutine = StartCoroutine(BattleTimerTick());
    }

    private void StopBattleTimer()
    {
      if (battleTimerRoutine != null)
      {
        StopCoroutine(battleTimerRoutine);
        battleTimerRoutine = null;
      }
    }

    private IEnumerator BattleTimerTick()
    {
      var wait = new WaitForSeconds(1f);
      while (true)
      {
        yield return wait;
        battleTimerSeconds += 1f;
        combatUIData.SetBattleTimer(TimeSpan.FromSeconds(battleTimerSeconds).ToString(@"mm\:ss"));
        UpdateTurnInfoUI();
      }
    }

    private bool ItemRequiresTarget(ItemScriptableObject item)
    {
      if (item == null)
      {
        return false;
      }

      return item.effects.Any(effect => !effect.targetSelf);
    }

    private bool RequiresTarget(SkillScriptableObject skill)
    {
      if (skill == null)
      {
        return false;
      }

      return skill.targetType switch
      {
        TargetType.Self => false,
        TargetType.All => false,
        TargetType.AllAllies => false,
        TargetType.Multiple => true,
        TargetType.AllEnemies => true,
        _ => skill.canTargetEnemies || skill.canTargetAllies
      };
    }

    private IEnumerable<Combatant> GetTargetsForItem(ItemScriptableObject item, Combatant caster)
    {
      var requiresTarget = ItemRequiresTarget(item);
      if (!requiresTarget)
      {
        return new List<Combatant> { caster };
      }

      var effects = item.effects.Where(effect => !effect.targetSelf).ToList();
      if (effects.Count == 0)
      {
        return new List<Combatant> { caster };
      }

      var hasDamage = effects.Any(effect => effect.effectType == EffectType.Damage);
      var hasHeal = effects.Any(effect => effect.effectType == EffectType.Heal);

      if (hasDamage && hasHeal)
      {
        return playerTeam.Concat(enemyTeam).Where(combatant => combatant.IsAlive).ToList();
      }

      if (hasDamage)
      {
        return caster.IsPlayerControlled ? enemyTeam : playerTeam;
      }

      if (hasHeal)
      {
        return caster.IsPlayerControlled ? playerTeam : enemyTeam;
      }

      return new List<Combatant> { caster };
    }

    private IEnumerable<Combatant> GetTargetsForSkill(SkillScriptableObject skill, Combatant caster)
    {
      if (skill == null)
      {
        return new List<Combatant> { caster };
      }

      return skill.targetType switch
      {
        TargetType.Self => new List<Combatant> { caster },
        TargetType.AllAllies => caster.IsPlayerControlled ? playerTeam : enemyTeam,
        TargetType.AllEnemies => caster.IsPlayerControlled ? enemyTeam : playerTeam,
        TargetType.All => playerTeam.Concat(enemyTeam).Where(c => c.IsAlive).ToList(),
        TargetType.Multiple => caster.IsPlayerControlled ? enemyTeam : playerTeam,
        _ => BuildSkillTargetsByAffinity(skill, caster)
      };
    }

    private IEnumerable<Combatant> BuildSkillTargetsByAffinity(SkillScriptableObject skill, Combatant caster)
    {
      var results = new List<Combatant>();

      if (skill.canTargetSelf)
      {
        results.Add(caster);
      }

      if (skill.canTargetAllies)
      {
        results.AddRange(caster.IsPlayerControlled ? playerTeam : enemyTeam);
      }

      if (skill.canTargetEnemies)
      {
        results.AddRange(caster.IsPlayerControlled ? enemyTeam : playerTeam);
      }

      return results;
    }

    private void ConfigureListView(ListView listView, IList source, Action<VisualElement, int> bindAction, Action<IEnumerable<object>> selectionHandler)
    {
      if (listView == null)
      {
        return;
      }

      listView.makeItem = () => new Label { pickingMode = PickingMode.Position };
      listView.bindItem = bindAction;
      listView.selectionChanged += selectionHandler;
    }

    public void RegisterCombatantTemplate(Combatant combatant, CombatantTemplateScriptableObject template)
    {
      if (combatant == null || template == null)
      {
        return;
      }

      combatantTemplates[combatant] = template;
      UpdateCombatantDisplay(combatant);
    }

    public void GenerateTestCombatants(List<CombatantTemplateScriptableObject> playerTemplates, List<CombatantTemplateScriptableObject> enemyTemplates)
    {
      var players = new List<Combatant>();
      var enemies = new List<Combatant>();

      if (playerTemplates != null)
      {
        foreach (var template in playerTemplates)
        {
          players.Add(CreateTestCombatantFromTemplate(template, true));
        }
      }

      if (enemyTemplates != null)
      {
        foreach (var template in enemyTemplates)
        {
          enemies.Add(CreateTestCombatantFromTemplate(template, false));
        }
      }

      InitializeBattle(players, enemies);
    }

    public Combatant CreateTestCombatantFromTemplate(CombatantTemplateScriptableObject template, bool isPlayerTeam)
    {
      if (template == null)
      {
        return null;
      }

      var go = new GameObject($"Test_{template.displayName}");
      var combatant = go.AddComponent<Combatant>();
      combatant.InitializeFromTemplate(template);
      combatant.SetTeam(isPlayerTeam ? CombatTeam.Player : CombatTeam.Enemy);
      RegisterCombatantTemplate(combatant, template);

      return combatant;
    }

    public void SimulateCombatTurn()
    {
      if (currentTurnCombatant == null)
      {
        if (playerTeam.Count > 0)
        {
          SetCurrentTurnCombatant(playerTeam[0]);
        }
        else if (enemyTeam.Count > 0)
        {
          SetCurrentTurnCombatant(enemyTeam[0]);
        }
      }

      var targets = GetValidTargets(currentAction, currentTurnCombatant);
      if (targets.Count == 0)
      {
        return;
      }

      var target = targets[UnityEngine.Random.Range(0, targets.Count)];
      ExecuteAction(currentAction, currentTurnCombatant, target);
    }

    public void TestTargetSelection()
    {
      if (currentTurnCombatant == null)
      {
        return;
      }

      StartTargetSelection(currentAction);
    }

    public void TestSkillUsage()
    {
      if (currentTurnCombatant == null)
      {
        return;
      }

      ShowSkillModal(currentTurnCombatant);
    }

    public void TestItemUsage()
    {
      if (currentTurnCombatant == null)
      {
        return;
      }

      ShowItemModal(currentTurnCombatant);
    }

    private void HandleCombatTurnStart(ICombatant combatant)
    {
      if (combatant is Combatant concrete)
      {
        SetCurrentTurnCombatant(concrete);
      }

      combatUIData.SetActionText(string.Empty);
      UpdateTurnInfoUI();
    }

    private void HandleCombatTurnEnd(ICombatant combatant)
    {
      combatUIData.IncrementTurn();
      UpdateTurnInfoUI();
    }

    private void HandleActionExecuted(ICombatant combatant, ActionResult result)
    {
      if (result == null)
      {
        return;
      }

      AddCombatMessage(result.Message, result.IsSuccess ? MessageType.System : MessageType.System);
    }

    private class SlotVisualCache
    {
      public VisualElement Root;
      public Vector2Int GridPosition;
      public bool IsPlayerSlot;
      public Label NameLabel;
      public ProgressBar HealthBar;
      public VisualElement Portrait;
      public Combatant Combatant;
    }

    private class CombatantEventSubscription
    {
      public Action<int> DamagedHandler;
      public Action<int> HealedHandler;
      public Action DefeatedHandler;
      public Action<StatType, int, int> StatChangedHandler;
    }
  }
}


