using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Run;
using EchoesOfTheVoid.Core.Combat.Systems;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using EchoesOfTheVoid.Core.Inventory.Results;
using ItemData = EchoesOfTheVoid.Core.Inventory.ScriptableObjects.ItemScriptableObject;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EchoesOfTheVoid.UI.Combat {
  /// <summary>
  /// Main combat screen responsible for presenting combatant state, handling user interactions,
  /// and delegating to the combat system.
  /// </summary>
  public sealed class CombatScreen : UIScreen {
    [Header("Services")]
    [SerializeField] private CombatSystem _combatSystem;
    [SerializeField] private CombatRunController _runController;

    [Header("State")]
    [SerializeField] private CombatUIData _combatUIData = new();
    [SerializeField, Min(0f)] private float _autoAdvanceNextFloorDelay = 0.6f;

    private CombatantGridController _gridController;
    private CombatActionController _actionController;
    private CombatLogPresenter _logPresenter;

    private Label _turnCounterLabel;
    private Label _battleTimerLabel;
    private Label _currentActionLabel;
    private Label _currentTurnLabel;
    private Label _currentFloorLabel;

    private Combatant _currentTurnCombatant;
    private CombatActionType _currentAction = CombatActionType.Attack;
    private Combatant _selectedTarget;
    private bool _isSelectingTarget;
    private readonly List<Combatant> _currentValidTargets = new();

    private float _battleTimerSeconds;
    private Coroutine _battleTimerRoutine;
    private readonly WaitForSeconds _battleTimerTick = new(1f);

    private ItemData _pendingItem;
    private SkillSO _pendingSkill;

    private bool _eventsSubscribed;
    private bool _runEventsSubscribed;
    private Coroutine _autoAdvanceRoutine;

    protected override void SetupUI() {
      ResolveServices();

      if (_screenContainer != null) {
        _screenContainer.style.flexGrow = 1f;
        _screenContainer.style.flexShrink = 0f;
        _screenContainer.style.alignSelf = Align.Stretch;
        _screenContainer.style.width = new Length(100, LengthUnit.Percent);
        _screenContainer.style.height = new Length(100, LengthUnit.Percent);
      }

      _turnCounterLabel = FindLabel("turn-counter");
      _battleTimerLabel = FindLabel("battle-timer");
      _currentFloorLabel = FindLabel("current-floor");
      _currentActionLabel = FindLabel("current-action");
      _currentTurnLabel = FindLabel("current-turn-character");

      VisualElement playerGrid = FindElement<VisualElement>("player-grid");
      VisualElement enemyGrid = FindElement<VisualElement>("enemy-grid");
      ScrollView logScroll = FindElement<ScrollView>("combat-log");
      VisualElement logContainer = FindElement<VisualElement>("log-messages");

      _gridController = new CombatantGridController();
      _gridController.Initialize(playerGrid, enemyGrid);

      _actionController = new CombatActionController(_screenContainer);
      _actionController.Initialize();

      _logPresenter = new CombatLogPresenter(this, logScroll, logContainer);

      _screenContainer.dataSource = _combatUIData;
      UpdateTurnInfoUI();
      UpdateAutoAllButtonState();
      UpdateFloorInfoFromState();
    }

    protected override void BindEvents() {
      if (_gridController != null) {
        _gridController.OnCombatantClicked += HandleCombatantSlotClicked;
        _gridController.OnAutoToggleRequested += HandleCombatantAutoToggleRequested;
      }

      if (_actionController != null) {
        _actionController.ActionRequested += HandleActionRequested;
        _actionController.ItemSelected += HandleItemSelected;
        _actionController.SkillSelected += HandleSkillSelected;
        _actionController.ModalsClosed += HandleModalsClosed;
        _actionController.AutoAllRequested += HandleAutoAllRequested;
      }
    }

    protected override void OnShow() {
      base.OnShow();
      ResolveServices();
      SubscribeCombatSystem();
      SubscribeRunController();
      UpdateTurnInfoUI();
      UpdateFloorInfoFromState();
    }

    protected override void OnHide() {
      base.OnHide();
      UnsubscribeCombatSystem();
      UnsubscribeRunController();
      StopAutoAdvanceRoutine();
      StopBattleTimer();
      EndTargetSelection();
    }

    private void OnDestroy() {
      StopBattleTimer();
      UnsubscribeCombatSystem();
      UnsubscribeRunController();
      _gridController?.Dispose();
    }

    public void InitializeBattle(List<Combatant> players, List<Combatant> enemies) {
      ResetViewState();

      _gridController?.SetPlayerTeam(players);
      _gridController?.SetEnemyTeam(enemies);
      UpdateAutoAllButtonState();

      StartBattleTimer();

      if (_combatSystem != null && _combatSystem.CurrentTurnCombatant is Combatant current) {
        SetCurrentTurnCombatant(current);
      } else if (players != null && players.Count > 0) {
        SetCurrentTurnCombatant(players[0]);
      } else if (enemies != null && enemies.Count > 0) {
        SetCurrentTurnCombatant(enemies[0]);
      }
    }

    public void SetActivePlayer(Combatant combatant) {
      SetCurrentTurnCombatant(combatant);
    }

    public void HandleCombatAction(CombatActionType actionType, Combatant caster, Combatant target, object actionData = null) {
      ExecuteAction(actionType, caster, target, actionData);
    }

    public void RegisterCombatantTemplate(Combatant combatant, CombatantSO template) {
      _gridController?.RegisterCombatantTemplate(combatant, template);
    }

    public void GenerateTestCombatants(List<CombatantSO> playerTemplates, List<CombatantSO> enemyTemplates) {
      var players = new List<Combatant>();
      var enemies = new List<Combatant>();

      if (playerTemplates != null) {
        foreach (CombatantSO template in playerTemplates) {
          Combatant combatant = CreateTestCombatantFromTemplate(template, true);
          if (combatant != null) {
            players.Add(combatant);
          }
        }
      }

      if (enemyTemplates != null) {
        foreach (CombatantSO template in enemyTemplates) {
          Combatant combatant = CreateTestCombatantFromTemplate(template, false);
          if (combatant != null) {
            enemies.Add(combatant);
          }
        }
      }

      InitializeBattle(players, enemies);
    }

    public Combatant CreateTestCombatantFromTemplate(CombatantSO template, bool isPlayerTeam) {
      if (template == null) {
        return null;
      }

      var go = new GameObject($"Test_{template.DisplayName}");
      Combatant combatant = go.AddComponent<Combatant>();
      combatant.InitializeFromTemplate(template);
      combatant.SetTeam(isPlayerTeam ? CombatTeam.Player : CombatTeam.Enemy);
      RegisterCombatantTemplate(combatant, template);

      return combatant;
    }

    public void SimulateCombatTurn() {
      if (_currentTurnCombatant == null) {
        IReadOnlyList<Combatant> playerTeam = _gridController?.PlayerTeam ?? Array.Empty<Combatant>();
        IReadOnlyList<Combatant> enemyTeam = _gridController?.EnemyTeam ?? Array.Empty<Combatant>();

        if (playerTeam.Count > 0) {
          SetCurrentTurnCombatant(playerTeam[0]);
        } else if (enemyTeam.Count > 0) {
          SetCurrentTurnCombatant(enemyTeam[0]);
        }
      }

      if (_currentTurnCombatant == null) {
        return;
      }

      IReadOnlyList<Combatant> validTargets = CombatTargetingService.GetValidTargets(
        _currentAction,
        _currentTurnCombatant,
        _pendingItem,
        _pendingSkill,
        _gridController?.PlayerTeam,
        _gridController?.EnemyTeam);

      if (validTargets.Count == 0) {
        return;
      }

      Combatant target = validTargets[UnityEngine.Random.Range(0, validTargets.Count)];
      ExecuteAction(_currentAction, _currentTurnCombatant, target);
    }

    private void ResolveServices() {
      if (_combatSystem == null) {
        _combatSystem = CombatSystem.Instance;
      }

      ResolveRunController();
    }

    private void ResolveRunController() {
      if (_runController == null) {
        _runController = FindFirstObjectByType<CombatRunController>();
      }
    }

    private void SubscribeCombatSystem() {
      if (_combatSystem == null || _eventsSubscribed) {
        return;
      }

      _combatSystem.OnTurnStart += HandleCombatTurnStart;
      _combatSystem.OnTurnEnd += HandleCombatTurnEnd;
      _combatSystem.OnActionExecuted += HandleActionExecuted;

      if (_combatSystem.StatusEffectManager != null) {
        _combatSystem.StatusEffectManager.OnEffectTicked += HandleStatusEffectTick;
      }

      _eventsSubscribed = true;
    }

    private void UnsubscribeCombatSystem() {
      if (_combatSystem == null || !_eventsSubscribed) {
        return;
      }

      _combatSystem.OnTurnStart -= HandleCombatTurnStart;
      _combatSystem.OnTurnEnd -= HandleCombatTurnEnd;
      _combatSystem.OnActionExecuted -= HandleActionExecuted;

      if (_combatSystem.StatusEffectManager != null) {
        _combatSystem.StatusEffectManager.OnEffectTicked -= HandleStatusEffectTick;
      }

      _eventsSubscribed = false;
    }

    private void SubscribeRunController() {
      if (_runEventsSubscribed) {
        return;
      }

      ResolveRunController();
      if (_runController == null) {
        return;
      }

      _runController.OnFloorStarted -= HandleRunFloorStarted;
      _runController.OnFloorCompleted -= HandleRunFloorCompleted;
      _runController.OnRunCompleted -= HandleRunCompleted;
      _runController.OnRunCancelled -= HandleRunCancelled;

      _runController.OnFloorStarted += HandleRunFloorStarted;
      _runController.OnFloorCompleted += HandleRunFloorCompleted;
      _runController.OnRunCompleted += HandleRunCompleted;
      _runController.OnRunCancelled += HandleRunCancelled;
      _runEventsSubscribed = true;
      UpdateFloorInfoFromState();
    }

    private void UnsubscribeRunController() {
      if (_runController == null || !_runEventsSubscribed) {
        return;
      }

      _runController.OnFloorStarted -= HandleRunFloorStarted;
      _runController.OnFloorCompleted -= HandleRunFloorCompleted;
      _runController.OnRunCompleted -= HandleRunCompleted;
      _runController.OnRunCancelled -= HandleRunCancelled;
      _runEventsSubscribed = false;
    }

    private void HandleActionRequested(CombatActionType actionType) {
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
          ShowItemSelection(_currentTurnCombatant);
          break;
        case CombatActionType.Skill:
          ShowSkillSelection(_currentTurnCombatant);
          break;
      }
    }

    private void ShowItemSelection(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      InventoryComponent inventory = combatant.GetComponent<InventoryComponent>();
      List<ItemData> items = inventory?.GetUsableItems()?.Where(static item => item != null).ToList() ?? new List<ItemData>();

      string emptyMessage = null;
      if (inventory == null) {
        emptyMessage = "This combatant cannot use items.";
      } else if (items.Count == 0) {
        emptyMessage = "No usable items available.";
      }

      _actionController?.ShowItemModal(items, emptyMessage);

      if (inventory == null || items.Count == 0) {
        AddCombatMessage("No items available.", MessageType.System);
        return;
      }

      // Items are available; allow the player to choose.
    }

    private void ShowSkillSelection(Combatant combatant) {
      if (combatant == null) {
        return;
      }

      SkillComponent skillComponent = combatant.GetComponent<SkillComponent>();
      if (skillComponent == null) {
        AddCombatMessage("No skills available.", MessageType.System);
        return;
      }

      List<CombatSkill> combatSkills = skillComponent.GetAllSkills()
        .Where(static skill => skill?.Data != null)
        .ToList();

      if (combatSkills.Count == 0) {
        AddCombatMessage("No skills available.", MessageType.System);
        return;
      }

      var skillEntries = new List<SkillSO>(combatSkills.Count);
      var stateBySkillId = new Dictionary<string, CombatActionController.SkillOptionState>(combatSkills.Count);

      foreach (CombatSkill combatSkill in combatSkills) {
        SkillSO skillData = combatSkill.Data;
        if (skillData == null || string.IsNullOrEmpty(skillData.SkillId)) {
          continue;
        }

        skillEntries.Add(skillData);

        int cooldownTurns = Mathf.Max(0, skillComponent.GetSkillCooldown(skillData.SkillId));
        bool hasMana = combatant.GetStat(StatType.Mana) >= skillData.ManaCost;
        bool isAvailable = skillComponent.CanUseSkill(skillData.SkillId);
        string unavailableReason = string.Empty;

        if (!isAvailable) {
          if (cooldownTurns > 0) {
            string turnLabel = cooldownTurns == 1 ? "turn" : "turns";
            unavailableReason = $"Available in {cooldownTurns} {turnLabel}.";
          } else if (!hasMana) {
            unavailableReason = "Not enough mana.";
          } else if (!combatant.IsAlive) {
            unavailableReason = "Combatant is unable to act.";
          } else {
            unavailableReason = "Currently unavailable.";
          }
        }

        stateBySkillId[skillData.SkillId] = new CombatActionController.SkillOptionState(
          isAvailable,
          cooldownTurns,
          unavailableReason
        );
      }

      if (skillEntries.Count == 0) {
        AddCombatMessage("No skills available.", MessageType.System);
        return;
      }

      _actionController?.ShowSkillModal(skillEntries, stateBySkillId);
    }

    private void HandleItemSelected(ItemData item) {
      _pendingItem = item;

      if (_currentTurnCombatant == null) {
        return;
      }

      bool requiresTarget = CombatTargetingService.ItemRequiresTarget(item);
      if (requiresTarget) {
        StartTargetSelection(CombatActionType.Item);
      } else {
        ExecuteAction(CombatActionType.Item, _currentTurnCombatant, _currentTurnCombatant, item);
      }
    }

    private void HandleSkillSelected(SkillSO skill) {
      _pendingSkill = skill;

      if (_currentTurnCombatant == null) {
        return;
      }

      bool requiresTarget = CombatTargetingService.SkillRequiresTarget(skill);
      if (requiresTarget) {
        StartTargetSelection(CombatActionType.Skill);
      } else {
        ExecuteAction(CombatActionType.Skill, _currentTurnCombatant, _currentTurnCombatant, skill);
      }
    }

    private void HandleModalsClosed() {
      _pendingItem = null;
      _pendingSkill = null;
      EndTargetSelection();
    }

    private void HandleCombatantAutoToggleRequested(Combatant combatant, bool enableAuto) {
      if (combatant == null) {
        return;
      }

      bool previousState = combatant.IsAutoCombatEnabled;
      SetCombatantAutoState(combatant, enableAuto);

      if (previousState == combatant.IsAutoCombatEnabled) {
        return;
      }

      if (combatant == _currentTurnCombatant && combatant.IsAutoCombatEnabled) {
        EndTargetSelection();
      }

      UpdateAutoAllButtonState();
      _actionController?.RefreshActionAvailability(_currentTurnCombatant);
    }

    private void HandleAutoAllRequested() {
      IReadOnlyList<Combatant> playerTeam = _gridController?.PlayerTeam;
      if (playerTeam == null || playerTeam.Count == 0) {
        return;
      }

      bool enableAll = playerTeam.Any(static combatant => combatant != null && !combatant.IsAutoCombatEnabled);
      bool shouldRefresh = false;
      bool endSelection = false;

      foreach (Combatant combatant in playerTeam) {
        if (combatant == null) {
          continue;
        }

        bool wasAuto = combatant.IsAutoCombatEnabled;
        SetCombatantAutoState(combatant, enableAll);

        if (wasAuto != combatant.IsAutoCombatEnabled) {
          shouldRefresh = true;
          if (combatant == _currentTurnCombatant && combatant.IsAutoCombatEnabled) {
            endSelection = true;
          }
        }
      }

      if (!shouldRefresh) {
        return;
      }

      if (endSelection) {
        EndTargetSelection();
      }

      UpdateAutoAllButtonState();
      _actionController?.RefreshActionAvailability(_currentTurnCombatant);
    }

    private void StartTargetSelection(CombatActionType actionType) {
      if (_currentTurnCombatant == null) {
        return;
      }

      _currentAction = actionType;
      _isSelectingTarget = true;
      _selectedTarget = null;

      RefreshTargetHighlights();

      if (_currentValidTargets.Count == 0) {
        AddCombatMessage("No valid targets available.", MessageType.System);
        EndTargetSelection();
      }
    }

    private void EndTargetSelection() {
      _isSelectingTarget = false;
      _selectedTarget = null;
      _currentValidTargets.Clear();
      _gridController?.ClearTargetHighlights();
    }

    private void RefreshTargetHighlights() {
      if (!_isSelectingTarget || _currentTurnCombatant == null) {
        return;
      }

      _currentValidTargets.Clear();
      IReadOnlyList<Combatant> targets = CombatTargetingService.GetValidTargets(
        _currentAction,
        _currentTurnCombatant,
        _pendingItem,
        _pendingSkill,
        _gridController?.PlayerTeam,
        _gridController?.EnemyTeam);

      _currentValidTargets.AddRange(targets);
      _gridController?.HighlightTargets(_currentValidTargets, _selectedTarget);
    }

    private void HandleCombatantSlotClicked(Combatant combatant) {
      if (!_isSelectingTarget) {
        return;
      }

      if (combatant == null) {
        AddCombatMessage("Cannot target an empty slot.", MessageType.System);
        return;
      }

      if (!_currentValidTargets.Contains(combatant)) {
        AddCombatMessage($"{combatant.Name} is not a valid target.", MessageType.System);
        return;
      }

      _selectedTarget = combatant;
      _gridController?.SelectTarget(combatant);

      object actionData = _currentAction switch {
        CombatActionType.Item => _pendingItem,
        CombatActionType.Skill => _pendingSkill,
        _ => null
      };

      ExecuteAction(_currentAction, _currentTurnCombatant, combatant, actionData);
      EndTargetSelection();
    }

    private void ExecuteAction(CombatActionType actionType, Combatant caster, Combatant target, object actionData = null) {
      if (caster == null) {
        AddCombatMessage("Action requires an acting combatant.", MessageType.System);
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
          SkillSO skill = actionData as SkillSO ?? _pendingSkill;
          if (skill == null) {
            AddCombatMessage("Select a skill first.", MessageType.System);
            return;
          }

          combatAction.SkillId = skill.SkillId;
          bool requiresTarget = CombatTargetingService.SkillRequiresTarget(skill);
          if (requiresTarget && target == null) {
            AddCombatMessage("Skill requires a target.", MessageType.System);
            return;
          }

          if (!requiresTarget) {
            combatAction.Target = caster;
          }
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
      _actionController?.HideModals(false);
      RefreshTargetHighlights();
    }

    private void ApplyLocalActionSimulation(Combatant caster, CombatAction action) {
      switch (action.ActionType) {
        case CombatActionType.Attack:
          if (action.Target is Combatant targetCombatant) {
            int before = targetCombatant.GetStat(StatType.Health);
            int simulatedDamage = Mathf.Max(1, caster.GetStat(StatType.Attack) - targetCombatant.GetStat(StatType.Defense));
            targetCombatant.TakeDamage(simulatedDamage);
            int after = targetCombatant.GetStat(StatType.Health);
            int actualDamage = Math.Max(0, before - after);

            var effect = new CombatEffect {
              Type = EffectType.Damage,
              Target = targetCombatant,
              Value = simulatedDamage,
              AppliedValue = actualDamage
            };

            string message = CombatLogMessageBuilder.BuildActionMessage(
              caster?.Name,
              CombatActionType.Attack.ToString(),
              CombatLogMessageBuilder.BuildEffectSummaries(new[] { effect })
            );

            AddCombatMessage(message, ResolveMessageType(new[] { effect }));
          }
          break;
        case CombatActionType.Defend:
          caster.SetDefending(true);
          string defendMessage = CombatLogMessageBuilder.BuildActionMessage(
            caster?.Name,
            CombatActionType.Defend.ToString(),
            new[] { "is bracing for impact" }
          );
          AddCombatMessage(defendMessage, MessageType.System);
          break;
        case CombatActionType.Item:
          if (action.ItemData != null) {
            InventoryComponent inventory = caster.GetComponent<InventoryComponent>();
            ItemResult result = inventory?.UseItem(action.ItemData, action.Target);
            if (result != null) {
              AddCombatMessage(result.Message, ResolveMessageType(result.Effects));
            }
          }
          break;
        case CombatActionType.Skill:
          if (!string.IsNullOrEmpty(action.SkillId)) {
            SkillComponent skillComponent = caster.GetComponent<SkillComponent>();
            SkillResult result = skillComponent?.UseSkill(action.SkillId, action.Target);
            if (result != null) {
              AddCombatMessage(result.Message, ResolveMessageType(result.Effects));
            }
          }
          break;
      }
    }

    private void HandleCombatTurnStart(ICombatant combatant) {
      if (combatant is Combatant concrete) {
        SetCurrentTurnCombatant(concrete);
      }

      _combatUIData.SetActionText(string.Empty);
      UpdateTurnInfoUI();
      UpdateAutoAllButtonState();
    }

    private void HandleCombatTurnEnd(ICombatant combatant) {
      _combatUIData.IncrementTurn();
      UpdateTurnInfoUI();
      UpdateAutoAllButtonState();
    }

    private void HandleActionExecuted(ICombatant combatant, ActionResult result) {
      if (result == null) {
        return;
      }

      MessageType type = result.IsSuccess ? ResolveMessageType(result.Effects) : MessageType.System;
      AddCombatMessage(result.Message, type);
    }

    private void HandleStatusEffectTick(ICombatant target, StatusEffect effect, int amount) {
      if (target is not Combatant combatant) {
        return;
      }

      _gridController?.UpdateCombatant(combatant);

      if (effect == null || amount <= 0) {
        return;
      }

      string message = CombatLogMessageBuilder.BuildStatusEffectTickMessage(combatant.Name, effect, amount);
      MessageType type = effect.EffectType == StatusEffectType.HealOverTime ? MessageType.Healing : MessageType.Damage;
      AddCombatMessage(message, type);
    }

    private void SetCurrentTurnCombatant(Combatant combatant) {
      _currentTurnCombatant = combatant;
      _combatUIData.SetTurnInfo(combatant);
      UpdateTurnInfoUI();
      _gridController?.SetCurrentTurn(combatant);
      RefreshTargetHighlights();
      UpdateAutoAllButtonState();
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

      if (_currentFloorLabel != null) {
        _currentFloorLabel.text = _combatUIData.CurrentFloorText;
      }

      _actionController?.RefreshActionAvailability(_currentTurnCombatant);
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
      while (true) {
        yield return _battleTimerTick;
        _battleTimerSeconds += 1f;
        _combatUIData.SetBattleTimer(TimeSpan.FromSeconds(_battleTimerSeconds).ToString(@"mm\:ss"));
        UpdateTurnInfoUI();
      }
    }

    private void SetCombatantAutoState(Combatant combatant, bool enableAuto) {
      if (combatant == null || combatant.IsAutoCombatEnabled == enableAuto) {
        return;
      }

      if (_combatSystem != null) {
        _combatSystem.SetAutoCombatEnabled(combatant, enableAuto);
      } else {
        combatant.SetAutoCombatEnabled(enableAuto);
      }

      _gridController?.UpdateCombatant(combatant);
    }

    private void UpdateAutoAllButtonState() {
      IReadOnlyList<Combatant> playerTeam = _gridController?.PlayerTeam;
      bool hasPlayers = false;
      bool allAuto = true;

      if (playerTeam != null) {
        foreach (Combatant combatant in playerTeam) {
          if (combatant == null) {
            continue;
          }

          hasPlayers = true;
          if (!combatant.IsAutoCombatEnabled) {
            allAuto = false;
          }
        }
      } else {
        allAuto = false;
      }

      if (!hasPlayers) {
        allAuto = false;
      }

      _actionController?.SetAutoAllState(allAuto, hasPlayers);
    }

    private void UpdateFloorInfoFromState() {
      string floorText = ResolveCurrentFloorText();
      _combatUIData.SetFloorInfo(floorText);

      if (_currentFloorLabel != null) {
        _currentFloorLabel.text = _combatUIData.CurrentFloorText;
      }
    }

    private string ResolveCurrentFloorText() {
      CombatRunState state = _runController?.State;
      if (state == null || state.Definition == null || !state.IsActive) {
        return string.Empty;
      }

      int totalFloors = Mathf.Max(0, state.Definition.FloorCount);
      if (totalFloors == 0) {
        return string.Empty;
      }

      int currentIndex = state.CurrentFloorIndex;
      bool hasActiveFloor = currentIndex >= 0 && currentIndex < totalFloors;
      int displayIndex = hasActiveFloor ? currentIndex + 1 : Mathf.Clamp(totalFloors > 0 ? 1 : 0, 0, totalFloors);

      string floorName = hasActiveFloor ? state.CurrentFloor?.DisplayName : null;
      if (!hasActiveFloor && totalFloors > 0 && string.IsNullOrWhiteSpace(floorName)) {
        floorName = state.Definition.GetFloor(0)?.DisplayName;
      }

      if (displayIndex <= 0) {
        return string.Empty;
      }

      string label = $"Floor {displayIndex}/{totalFloors}";
      if (!string.IsNullOrWhiteSpace(floorName)) {
        label = $"{label} · {floorName}";
      }

      return label;
    }

    private void ResetViewState() {
      EndTargetSelection();
      _actionController?.HideModals(false);
      _battleTimerSeconds = 0f;
      _combatUIData.Reset();
      UpdateTurnInfoUI();
      _gridController?.Clear();
      _logPresenter?.Clear();
      _currentTurnCombatant = null;
      _pendingItem = null;
      _pendingSkill = null;
      StopAutoAdvanceRoutine();
      UpdateAutoAllButtonState();
      UpdateFloorInfoFromState();
    }

    private void AddCombatMessage(string message, MessageType messageType) {
      _logPresenter?.AddMessage(message, messageType);
    }

    private MessageType ResolveMessageType(IReadOnlyCollection<CombatEffect> effects) {
      if (effects == null || effects.Count == 0) {
        return MessageType.System;
      }

      bool hasDamage = false;
      bool hasHealing = false;

      foreach (CombatEffect effect in effects) {
        if (effect == null) {
          continue;
        }

        if (effect.Type == EffectType.Damage && effect.AppliedValue > 0) {
          hasDamage = true;
        } else if (effect.Type == EffectType.Heal && effect.AppliedValue > 0) {
          hasHealing = true;
        }
      }

      if (hasDamage && !hasHealing) {
        return MessageType.Damage;
      }

      if (hasHealing && !hasDamage) {
        return MessageType.Healing;
      }

      return MessageType.System;
    }

    private void HandleRunFloorStarted(CombatRunFloorDefinition floor, int floorIndex, IReadOnlyList<Combatant> playerParty, IReadOnlyList<Combatant> enemies) {
      UpdateFloorInfoFromState();
    }

    private void HandleRunFloorCompleted(CombatRunFloorResult result) {
      StopAutoAdvanceRoutine();
      UpdateFloorInfoFromState();

      if (!ShouldAutoAdvanceToNextFloor(result)) {
        return;
      }

      _autoAdvanceRoutine = StartCoroutine(AutoAdvanceNextFloorRoutine());
    }

    private void HandleRunCompleted(CombatRunState state) {
      StopAutoAdvanceRoutine();
      UpdateFloorInfoFromState();
      NavigateBackToSelection();
    }

    private void HandleRunCancelled(CombatRunState state) {
      StopAutoAdvanceRoutine();
      UpdateFloorInfoFromState();
      NavigateBackToSelection();
    }

    private IEnumerator AutoAdvanceNextFloorRoutine() {
      float elapsed = 0f;

      while (_runController != null && !_runController.HasPendingNextFloor) {
        elapsed += Time.deltaTime;
        yield return null;
      }

      if (_runController == null) {
        _autoAdvanceRoutine = null;
        yield break;
      }

      float remainingDelay = Mathf.Max(0f, _autoAdvanceNextFloorDelay - elapsed);
      if (remainingDelay > 0f) {
        yield return new WaitForSeconds(remainingDelay);
      }

      if (_runController != null && _runController.HasPendingNextFloor) {
        _runController.ProceedToNextFloor();
      }

      _autoAdvanceRoutine = null;
    }

    private bool ShouldAutoAdvanceToNextFloor(CombatRunFloorResult result) {
      if (_runController?.State == null || result == null) {
        return false;
      }

      if (result.Outcome != CombatOutcome.Victory) {
        return false;
      }

      CombatRunState state = _runController.State;
      CombatRunDefinition definition = state.Definition;
      if (definition == null) {
        return false;
      }

      // If we've already recorded results for all floors, don't auto advance.
      return state.FloorResults.Count < definition.FloorCount;
    }

    private void StopAutoAdvanceRoutine() {
      if (_autoAdvanceRoutine != null) {
        StopCoroutine(_autoAdvanceRoutine);
        _autoAdvanceRoutine = null;
      }
    }

    private void NavigateBackToSelection() {
      if (global::NavigationManager.Instance != null && global::NavigationManager.Instance.IsScreenActive(_screenId)) {
        global::NavigationManager.Instance.NavigateBack();
      }
    }

#if UNITY_EDITOR
    private void OnValidate() {
      if (string.IsNullOrEmpty(_screenId)) {
        _screenId = "CombatScreen";
      }

      if (_screenTemplate == null) {
        _screenTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/Screens/Combat/CombatView.uxml");
      }
    }
#endif
  }
}
