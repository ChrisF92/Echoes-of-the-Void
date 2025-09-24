using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Gambits;
using EchoesOfTheVoid.Core.Combat.Managers;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.Turn;
using EchoesOfTheVoid.Core.Combat.Extensions;

namespace EchoesOfTheVoid.Core.Combat.Systems
{
  public class CombatSystem : MonoBehaviour
  {
    public static CombatSystem Instance { get; private set; }

    [Header("Combat Settings")]
    [SerializeField] private float _turnDelay = 1f;
    [SerializeField] private int _maxPlayersPerSide = 9;

    private CombatState _currentState = CombatState.Setup;
    private readonly List<ICombatant> _allCombatants = new();
    private readonly List<ICombatant> _playerTeam = new();
    private readonly List<ICombatant> _enemyTeam = new();
    private readonly System.Random _random = new();
    private TurnOrderManager _turnOrderManager;
    private CombatantManager _combatantManager;

    public CombatState CurrentState => _currentState;
    public ICombatant CurrentTurnCombatant => _turnOrderManager?.CurrentCombatant;
    public List<ICombatant> PlayerTeam => _playerTeam;
    public List<ICombatant> EnemyTeam => _enemyTeam;

    public event Action<CombatState> OnStateChanged;
    public event Action<ICombatant> OnTurnStart;
    public event Action<ICombatant> OnTurnEnd;
    public event Action<CombatResult> OnCombatEnd;
    public event Action<ICombatant, ActionResult> OnActionExecuted;
    public event Action<GambitEvaluationLog> OnGambitEvaluated;

    private void Awake()
    {
      if (Instance == null)
      {
        Instance = this;
        InitializeManagers();
      }
      else
      {
        Destroy(gameObject);
      }
    }

    private void InitializeManagers()
    {
      _turnOrderManager = new TurnOrderManager();
      _combatantManager = new CombatantManager();

      _turnOrderManager.OnTurnStart += HandleTurnStart;
      _turnOrderManager.OnTurnEnd += HandleTurnEnd;
      _combatantManager.OnCombatantDefeated += HandleCombatantDefeated;
    }

    public void StartCombat(List<ICombatant> players, List<ICombatant> enemies)
    {
      if (players.Count > _maxPlayersPerSide || enemies.Count > _maxPlayersPerSide)
      {
        Debug.LogError($"Too many combatants! Max per side: {_maxPlayersPerSide}");
        return;
      }

      SetupCombat(players, enemies);
      ChangeState(CombatState.InProgress);
      _turnOrderManager.StartCombat(_allCombatants);
    }

    private void SetupCombat(List<ICombatant> players, List<ICombatant> enemies)
    {
      _playerTeam.Clear();
      _enemyTeam.Clear();
      _allCombatants.Clear();

      foreach (var player in players)
      {
        player.SetTeam(CombatTeam.Player);
        _playerTeam.Add(player);
        _allCombatants.Add(player);
        _combatantManager.RegisterCombatant(player);
      }

      foreach (var enemy in enemies)
      {
        enemy.SetTeam(CombatTeam.Enemy);
        _enemyTeam.Add(enemy);
        _allCombatants.Add(enemy);
        _combatantManager.RegisterCombatant(enemy);
      }
    }

    public void SetAutoCombatEnabled(ICombatant combatant, bool enabled)
    {
      if (combatant == null)
      {
        return;
      }

      if (combatant.IsAutoCombatEnabled == enabled)
      {
        return;
      }

      combatant.SetAutoCombatEnabled(enabled);

      if (enabled && combatant == CurrentTurnCombatant && ShouldAutoAct(combatant))
      {
        TryExecuteAiTurn(combatant);
      }
    }

    public void SetGambitProfile(ICombatant combatant, IGambitRuleSource profile)
    {
      if (combatant is Combatant concrete)
      {
        concrete.ApplyGambitProfile(profile);
      }
    }

    public void SetGambitProfile(ICombatant combatant, GambitProfileData profile)
    {
      SetGambitProfile(combatant, profile as IGambitRuleSource);
    }

    public bool ExecuteAction(ICombatant actor, CombatAction action)
    {
      if (_currentState != CombatState.InProgress)
      {
        Debug.LogWarning("Cannot execute action outside of combat");
        return false;
      }

      if (actor != CurrentTurnCombatant)
      {
        Debug.LogWarning($"It's not {actor.Name}'s turn");
        return false;
      }

      var result = ProcessAction(actor, action);
      OnActionExecuted?.Invoke(actor, result);

      if (result.IsSuccess)
      {
        _turnOrderManager.EndCurrentTurn();
        return true;
      }

      return false;
    }

    private ActionResult ProcessAction(ICombatant actor, CombatAction action)
    {
      return action.ActionType switch
      {
        CombatActionType.Attack => ProcessAttackAction(actor, action),
        CombatActionType.Defend => ProcessDefendAction(actor, action),
        CombatActionType.Skill => ProcessSkillAction(actor, action),
        CombatActionType.Item => ProcessItemAction(actor, action),
        _ => ActionResult.Failed("Unknown action type")
      };
    }

    private ActionResult ProcessAttackAction(ICombatant actor, CombatAction action)
    {
      if (action.Target == null || !action.Target.IsAlive)
      {
        return ActionResult.Failed("Invalid target");
      }

      var damage = CalculateAttackDamage(actor, action.Target);
      action.Target.TakeDamage(damage);

      return ActionResult.Success($"{actor.Name} attacks {action.Target.Name} for {damage} damage!");
    }

    private ActionResult ProcessDefendAction(ICombatant actor, CombatAction action)
    {
      actor.SetDefending(true);
      return ActionResult.Success($"{actor.Name} takes a defensive stance!");
    }

    private ActionResult ProcessSkillAction(ICombatant actor, CombatAction action)
    {
      var skillComponent = actor.GetComponent<SkillComponent>();
      if (skillComponent == null)
      {
        return ActionResult.Failed("Actor has no skills");
      }

      return skillComponent.UseSkill(action.SkillId, action.Target).ToActionResult();
    }

    private ActionResult ProcessItemAction(ICombatant actor, CombatAction action)
    {
      var inventoryComponent = actor.GetComponent<InventoryComponent>();
      if (inventoryComponent == null)
      {
        return ActionResult.Failed("Actor has no inventory");
      }

      return inventoryComponent.UseItem(action.ItemData, action.Target).ToActionResult();
    }

    private int CalculateAttackDamage(ICombatant attacker, ICombatant target)
    {
      var baseDamage = attacker.GetStat(StatType.Attack);
      var defense = target.GetStat(StatType.Defense);

      if (target.IsDefending)
      {
        defense = Mathf.RoundToInt(defense * 1.5f);
      }

      var finalDamage = Math.Max(1, baseDamage - defense);
      var variance = UnityEngine.Random.Range(0.9f, 1.1f);
      return Mathf.RoundToInt(finalDamage * variance);
    }


    private void HandleTurnStart(ICombatant combatant)
    {
      combatant.SetDefending(false);
      combatant.UpdateComponents(Time.deltaTime);

      OnTurnStart?.Invoke(combatant);

      if (ShouldAutoAct(combatant))
      {
        TryExecuteAiTurn(combatant);
      }
    }

    private bool ShouldAutoAct(ICombatant combatant)
    {
      if (combatant == null || !combatant.IsAlive)
      {
        return false;
      }

      if (!combatant.IsPlayerControlled)
      {
        return true;
      }

      return combatant.IsAutoCombatEnabled;
    }

    private void TryExecuteAiTurn(ICombatant combatant)
    {
      if (_currentState != CombatState.InProgress || combatant != CurrentTurnCombatant)
      {
        return;
      }

      CombatAction selectedAction = null;
      GambitEvaluationLog evaluationLog = null;

      if (combatant is Combatant concreteCombatant)
      {
        var gambitComponent = concreteCombatant.GetComponent<GambitComponent>();
        if (gambitComponent != null)
        {
          var context = BuildGambitContext(concreteCombatant);
          if (!gambitComponent.TryBuildAction(context, out selectedAction, out evaluationLog))
          {
            if (evaluationLog == null)
            {
              evaluationLog = new GambitEvaluationLog(concreteCombatant, null);
            }
          }
        }
        else
        {
          evaluationLog = new GambitEvaluationLog(concreteCombatant, null);
          evaluationLog.Records.Add(new GambitRuleEvaluationRecord(null)
          {
            FailureReason = "No GambitComponent found on combatant"
          });
        }
      }
      else
      {
        Debug.LogWarning($"Gambit AI requires Combatant concrete type. Received: {combatant?.GetType().Name ?? "<null>"}");
      }

      if (selectedAction == null)
      {
        selectedAction = BuildFallbackAction(combatant, evaluationLog);
      }

      PublishGambitLog(evaluationLog);

      if (selectedAction == null)
      {
        _turnOrderManager.EndCurrentTurn();
        return;
      }

      if (!ExecuteAction(combatant, selectedAction))
      {
        _turnOrderManager.EndCurrentTurn();
      }
    }

    private GambitRuntimeContext BuildGambitContext(Combatant actor)
    {
      var allies = GetAllyTargets(actor);
      var enemies = GetEnemyTargets(actor);
      var turnNumber = _turnOrderManager?.CurrentRound ?? 1;
      return new GambitRuntimeContext(actor, allies, enemies, turnNumber, this, _random);
    }

    private CombatAction BuildFallbackAction(ICombatant actor, GambitEvaluationLog log)
    {
      var enemies = GetEnemyTargets(actor);
      if (enemies.Count == 0)
      {
        log?.Records.Add(new GambitRuleEvaluationRecord(null)
        {
          FailureReason = "Fallback failed: no valid enemy targets"
        });
        return null;
      }

      var target = enemies[_random.Next(enemies.Count)];
      var fallbackAction = new CombatAction
      {
        ActionType = CombatActionType.Attack,
        Target = target
      };

      if (log != null)
      {
        log.Records.Add(new GambitRuleEvaluationRecord(null)
        {
          ActionBuilt = true,
          Target = target,
          FailureReason = "Fallback action executed"
        });
        log.SetResult(fallbackAction, target);
      }

      return fallbackAction;
    }

    private void PublishGambitLog(GambitEvaluationLog log)
    {
      if (log == null)
      {
        return;
      }

      OnGambitEvaluated?.Invoke(log);
    }

    private void HandleTurnEnd(ICombatant combatant)
    {
      OnTurnEnd?.Invoke(combatant);
      CheckCombatEndConditions();
    }

    private void HandleCombatantDefeated(ICombatant combatant)
    {
      _turnOrderManager.RemoveCombatant(combatant);
      CheckCombatEndConditions();
    }

    private void CheckCombatEndConditions()
    {
      var alivePlayerCount = _playerTeam.Count(c => c.IsAlive);
      var aliveEnemyCount = _enemyTeam.Count(c => c.IsAlive);

      if (alivePlayerCount == 0)
      {
        EndCombat(new CombatResult(CombatOutcome.Defeat, _enemyTeam.Where(e => e.IsAlive).ToList()));
      }
      else if (aliveEnemyCount == 0)
      {
        EndCombat(new CombatResult(CombatOutcome.Victory, _playerTeam.Where(p => p.IsAlive).ToList()));
      }
    }

    private void EndCombat(CombatResult result)
    {
      ChangeState(CombatState.Ended);
      OnCombatEnd?.Invoke(result);
    }

    private void ChangeState(CombatState newState)
    {
      _currentState = newState;
      OnStateChanged?.Invoke(newState);
    }

    public List<ICombatant> GetValidTargets(ICombatant actor, TargetType targetType)
    {
      return targetType switch
      {
        TargetType.Single => GetEnemyTargets(actor),
        TargetType.Self => new List<ICombatant> { actor },
        TargetType.AllAllies => GetAllyTargets(actor),
        TargetType.AllEnemies => GetEnemyTargets(actor),
        TargetType.All => _allCombatants.Where(c => c.IsAlive).ToList(),
        _ => new List<ICombatant>()
      };
    }

    private List<ICombatant> GetAllyTargets(ICombatant actor)
    {
      var team = actor.Team == CombatTeam.Player ? _playerTeam : _enemyTeam;
      return team.Where(c => c.IsAlive).ToList();
    }

    private List<ICombatant> GetEnemyTargets(ICombatant actor)
    {
      var enemyTeam = actor.Team == CombatTeam.Player ? _enemyTeam : _playerTeam;
      return enemyTeam.Where(c => c.IsAlive).ToList();
    }
  }
}
