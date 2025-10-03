using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Managers;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.Turn;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Core combat orchestrator. Delegates specific responsibilities to specialized systems.
  /// </summary>
  public class CombatSystem : MonoBehaviour {
    public static CombatSystem Instance { get; private set; }

    [Header("Combat Settings")]
    [SerializeField] private int _maxPlayersPerSide = 9;
    [SerializeField] private CombatActionTiming _defaultActionTiming = CombatActionTiming.Default;
    [SerializeField] private List<CombatActionTimingOverride> _actionTimingOverrides = new();
    [SerializeField, Min(0f)] private float _autoDecisionDelay = 0.3f;

    // Core systems
    private CombatStateManager _stateManager;
    private TurnOrderManager _turnOrderManager;
    private CombatantManager _combatantManager;
    private ActionExecutor _actionExecutor;
    private AIDecisionSystem _aiDecisionSystem;
    private TargetResolver _targetResolver;

    // State
    private readonly List<ICombatant> _allCombatants = new();
    private readonly List<ICombatant> _playerTeam = new();
    private readonly List<ICombatant> _enemyTeam = new();
    private readonly System.Random _random = new();

    // Properties
    public CombatState CurrentState => _stateManager?.CurrentState ?? CombatState.Setup;
    public ICombatant CurrentTurnCombatant => _turnOrderManager?.CurrentCombatant;
    public IReadOnlyList<ICombatant> PlayerTeam => _playerTeam;
    public IReadOnlyList<ICombatant> EnemyTeam => _enemyTeam;
    public DamageCalculator DamageCalculator { get; private set; }
    public StatusEffectManager StatusEffectManager { get; private set; }

    // Events
    public event Action<CombatState> OnStateChanged;
    public event Action<ICombatant> OnTurnStart;
    public event Action<ICombatant> OnTurnEnd;
    public event Action<CombatResult> OnCombatEnd;
    public event Action<ICombatant, ActionResult> OnActionExecuted;
    public event Action<ICombatant, CombatAction, CombatActionPhase> OnActionPhase;
    public event Action<GambitEvaluationLog> OnGambitEvaluated;

    private void Awake() {
      if (Instance != null) {
        Destroy(gameObject);
        return;
      }

      Instance = this;
      InitializeSystems();
    }

    private void InitializeSystems() {
      EnsureTimingDefaults();

      DamageCalculator = new DamageCalculator();
      StatusEffectManager = new StatusEffectManager();
      _stateManager = new CombatStateManager();
      _turnOrderManager = new TurnOrderManager();
      _combatantManager = new CombatantManager();
      _targetResolver = new TargetResolver(_playerTeam, _enemyTeam, _allCombatants);

      var timingProvider = new ActionTimingProvider(_defaultActionTiming, _actionTimingOverrides);
      _actionExecutor = new ActionExecutor(
        this,
        _turnOrderManager,
        DamageCalculator,
        StatusEffectManager,
        timingProvider
      );

      _aiDecisionSystem = new AIDecisionSystem(
        this,
        _targetResolver,
        _autoDecisionDelay,
        _random
      );

      // Wire up events
      _stateManager.OnStateChanged += state => OnStateChanged?.Invoke(state);
      _turnOrderManager.OnTurnStart += HandleTurnStart;
      _turnOrderManager.OnTurnEnd += HandleTurnEnd;
      _combatantManager.OnCombatantDefeated += HandleCombatantDefeated;
      _actionExecutor.OnActionExecuted += (actor, result) => OnActionExecuted?.Invoke(actor, result);
      _actionExecutor.OnActionPhase += (actor, action, phase) => OnActionPhase?.Invoke(actor, action, phase);
      _aiDecisionSystem.OnGambitEvaluated += log => OnGambitEvaluated?.Invoke(log);
    }

    private void EnsureTimingDefaults() {
      if (_defaultActionTiming.Total <= 0f) {
        _defaultActionTiming = CombatActionTiming.Default;
      }
    }

    public void StartCombat(List<ICombatant> players, List<ICombatant> enemies) {
      if (players.Count > _maxPlayersPerSide || enemies.Count > _maxPlayersPerSide) {
        Debug.LogError($"Too many combatants! Max per side: {_maxPlayersPerSide}");
        return;
      }

      SetupCombat(players, enemies);
      _stateManager.ChangeState(CombatState.InProgress);
      _turnOrderManager.StartCombat(_allCombatants);
    }

    private void SetupCombat(List<ICombatant> players, List<ICombatant> enemies) {
      _playerTeam.Clear();
      _enemyTeam.Clear();
      _allCombatants.Clear();

      foreach (ICombatant player in players) {
        player.SetTeam(CombatTeam.Player);
        _playerTeam.Add(player);
        _allCombatants.Add(player);
        _combatantManager.RegisterCombatant(player);
      }

      foreach (ICombatant enemy in enemies) {
        enemy.SetTeam(CombatTeam.Enemy);
        _enemyTeam.Add(enemy);
        _allCombatants.Add(enemy);
        _combatantManager.RegisterCombatant(enemy);
      }
    }

    public bool ExecuteAction(ICombatant actor, CombatAction action) {
      if (!_stateManager.CanExecuteActions()) {
        Debug.LogWarning("Cannot execute action in current state");
        return false;
      }

      if (actor != CurrentTurnCombatant) {
        Debug.LogWarning($"It's not {actor?.Name ?? "<null>"}'s turn");
        return false;
      }

      return _actionExecutor.QueueAction(actor, action);
    }

    public void SetAutoCombatEnabled(ICombatant combatant, bool enabled) {
      if (combatant == null) {
        return;
      }

      combatant.SetAutoCombatEnabled(enabled);
      _aiDecisionSystem.OnAutoCombatChanged(combatant, enabled);
    }

    public void SetGambitProfile(ICombatant combatant, IGambitRuleSource profile) {
      if (combatant is Combatant concrete) {
        concrete.ApplyGambitProfile(profile);
      }
    }

    public List<ICombatant> GetValidTargets(ICombatant actor, TargetType targetType) {
      return _targetResolver.GetValidTargets(actor, targetType);
    }

    private void HandleTurnStart(ICombatant combatant) {
      combatant.SetDefending(false);
      StatusEffectManager.ProcessTurnStart(combatant);
      combatant.UpdateComponents(Time.deltaTime);

      OnTurnStart?.Invoke(combatant);

      if (ShouldAutoAct(combatant)) {
        _aiDecisionSystem.ScheduleAutoAction(combatant);
      }
    }

    private void HandleTurnEnd(ICombatant combatant) {
      StatusEffectManager.ProcessTurnEnd(combatant);
      OnTurnEnd?.Invoke(combatant);
      CheckCombatEndConditions();
    }

    private void HandleCombatantDefeated(ICombatant combatant) {
      _turnOrderManager.RemoveCombatant(combatant);
      StatusEffectManager.ClearEffects(combatant);
      CheckCombatEndConditions();
    }

    private bool ShouldAutoAct(ICombatant combatant) {
      return combatant != null &&
             combatant.IsAlive &&
             (!combatant.IsPlayerControlled || combatant.IsAutoCombatEnabled);
    }

    private void CheckCombatEndConditions() {
      int alivePlayerCount = 0;
      int aliveEnemyCount = 0;

      foreach (ICombatant player in _playerTeam) {
        if (player.IsAlive) {
          alivePlayerCount++;
        }
      }

      foreach (ICombatant enemy in _enemyTeam) {
        if (enemy.IsAlive) {
          aliveEnemyCount++;
        }
      }

      if (alivePlayerCount == 0) {
        EndCombat(CombatOutcome.Defeat);
      } else if (aliveEnemyCount == 0) {
        EndCombat(CombatOutcome.Victory);
      }
    }

    private void EndCombat(CombatOutcome outcome) {
      var survivors = new List<ICombatant>();
      foreach (ICombatant combatant in _allCombatants) {
        if (combatant.IsAlive) {
          survivors.Add(combatant);
        }
      }

      var result = new CombatResult(outcome, survivors);
      _stateManager.ChangeState(CombatState.Ended);
      OnCombatEnd?.Invoke(result);
    }
  }
}