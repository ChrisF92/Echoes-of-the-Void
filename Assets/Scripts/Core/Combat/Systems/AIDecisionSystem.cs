using System;
using System.Collections;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Gambits;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Handles AI decision making and gambit evaluation.
  /// </summary>
  public class AIDecisionSystem {
    private readonly MonoBehaviour _coroutineHost;
    private readonly TargetResolver _targetResolver;
    private readonly float _autoDecisionDelay;
    private readonly System.Random _random;

    private Coroutine _autoActionRoutine;
    private ICombatant _autoActionTarget;

    public event Action<GambitEvaluationLog> OnGambitEvaluated;

    public AIDecisionSystem(
      MonoBehaviour host,
      TargetResolver resolver,
      float decisionDelay,
      System.Random rng) {
      _coroutineHost = host;
      _targetResolver = resolver;
      _autoDecisionDelay = decisionDelay;
      _random = rng;
    }

    public void ScheduleAutoAction(ICombatant combatant) {
      CancelAutoAction();

      if (!_coroutineHost.isActiveAndEnabled || _autoDecisionDelay <= 0f) {
        ExecuteAiTurn(combatant);
        return;
      }

      _autoActionTarget = combatant;
      _autoActionRoutine = _coroutineHost.StartCoroutine(AutoActionRoutine(combatant));
    }

    public void OnAutoCombatChanged(ICombatant combatant, bool enabled) {
      if (!enabled && _autoActionTarget == combatant) {
        CancelAutoAction();
      }
    }

    private void CancelAutoAction() {
      if (_autoActionRoutine != null) {
        _coroutineHost.StopCoroutine(_autoActionRoutine);
        _autoActionRoutine = null;
      }
      _autoActionTarget = null;
    }

    private IEnumerator AutoActionRoutine(ICombatant combatant) {
      if (_autoDecisionDelay > 0f) {
        yield return new WaitForSeconds(_autoDecisionDelay);
      }

      _autoActionRoutine = null;
      _autoActionTarget = null;

      if (combatant != null &&
          combatant == CombatSystem.Instance.CurrentTurnCombatant &&
          combatant.IsAlive) {
        ExecuteAiTurn(combatant);
      }
    }

    private void ExecuteAiTurn(ICombatant combatant) {
      if (CombatSystem.Instance.CurrentState != CombatState.InProgress) {
        return;
      }

      CancelAutoAction();

      CombatAction selectedAction = null;
      GambitEvaluationLog evaluationLog = null;

      if (combatant is Combatant concreteCombatant) {
        GambitComponent gambitComponent = concreteCombatant.GetComponent<GambitComponent>();
        if (gambitComponent != null) {
          GambitRuntimeContext context = BuildGambitContext(concreteCombatant);
          _ = gambitComponent.TryBuildAction(context, out selectedAction, out evaluationLog);
        } else {
          evaluationLog = new GambitEvaluationLog(concreteCombatant, null);
          evaluationLog.Records.Add(new GambitRuleEvaluationRecord(null) {
            FailureReason = "No GambitComponent"
          });
        }
      }

      selectedAction ??= BuildFallbackAction(combatant, evaluationLog);

      OnGambitEvaluated?.Invoke(evaluationLog);

      if (selectedAction != null) {
        _ = CombatSystem.Instance.ExecuteAction(combatant, selectedAction);
      }
    }

    private GambitRuntimeContext BuildGambitContext(Combatant actor) {
      List<ICombatant> allies = _targetResolver.GetAllyTargets(actor);
      List<ICombatant> enemies = _targetResolver.GetEnemyTargets(actor);
      int turnNumber = CombatSystem.Instance.CurrentTurnCombatant != null ? 1 : 1; // Get from turn manager if needed

      return new GambitRuntimeContext(
        actor,
        allies,
        enemies,
        turnNumber,
        CombatSystem.Instance,
        _random
      );
    }

    private CombatAction BuildFallbackAction(ICombatant actor, GambitEvaluationLog log) {
      List<ICombatant> enemies = _targetResolver.GetEnemyTargets(actor);
      if (enemies.Count == 0) {
        log?.Records.Add(new GambitRuleEvaluationRecord(null) {
          FailureReason = "Fallback: no valid targets"
        });
        return null;
      }

      ICombatant target = enemies[_random.Next(enemies.Count)];
      var fallbackAction = new CombatAction {
        ActionType = CombatActionType.Attack,
        Target = target
      };

      if (log != null) {
        log.Records.Add(new GambitRuleEvaluationRecord(null) {
          ActionBuilt = true,
          Target = target,
          FailureReason = "Fallback action"
        });
        log.SetResult(fallbackAction, target);
      }

      return fallbackAction;
    }
  }
}
