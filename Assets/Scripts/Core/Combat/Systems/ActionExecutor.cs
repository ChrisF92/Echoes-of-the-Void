using System;
using System.Collections;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Actions;
using EchoesOfTheVoid.Core.Combat.Components;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Extensions;
using EchoesOfTheVoid.Core.Combat.Results;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using EchoesOfTheVoid.Core.Combat.Turn;
using EchoesOfTheVoid.Core.Combat.Wrappers;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Handles action queueing, validation, and execution.
  /// </summary>
  public class ActionExecutor {
    private readonly MonoBehaviour _coroutineHost;
    private readonly TurnOrderManager _turnOrderManager;
    private readonly DamageCalculator _damageCalculator;
    private readonly StatusEffectManager _statusEffectManager;
    private readonly ActionTimingProvider _timingProvider;
    private readonly TargetResolver _targetResolver;

    private readonly Queue<PendingAction> _actionQueue = new();
    private Coroutine _actionQueueRoutine;
    private bool _isProcessingAction;

    public event Action<ICombatant, ActionResult> OnActionExecuted;
    public event Action<ICombatant, CombatAction, CombatActionPhase> OnActionPhase;

    public ActionExecutor(
      MonoBehaviour host,
      TurnOrderManager turnManager,
      DamageCalculator damageCalc,
      StatusEffectManager statusManager,
      ActionTimingProvider timing,
      TargetResolver targetResolver) {
      _coroutineHost = host;
      _turnOrderManager = turnManager;
      _damageCalculator = damageCalc;
      _statusEffectManager = statusManager;
      _timingProvider = timing;
      _targetResolver = targetResolver;
    }

    public bool QueueAction(ICombatant actor, CombatAction action) {
      if (_isProcessingAction || _actionQueue.Count > 0) {
        Debug.LogWarning("Action already being processed");
        return false;
      }

      if (!ValidateAction(actor, action)) {
        return false;
      }

      _actionQueue.Enqueue(new PendingAction(actor, action));

      _actionQueueRoutine ??= _coroutineHost.StartCoroutine(ProcessActionQueue());

      return true;
    }

    private bool ValidateAction(ICombatant actor, CombatAction action) {
      if (actor == null || !actor.IsAlive) {
        Debug.LogWarning("Actor is invalid or defeated");
        return false;
      }

      if (action == null) {
        Debug.LogWarning("Action is null");
        return false;
      }

      switch (action.ActionType) {
        case CombatActionType.Attack:
          return ValidateTarget(action.Target);

        case CombatActionType.Defend:
          return true;

        case CombatActionType.Skill:
          return ValidateSkillAction(actor, action);

        case CombatActionType.Item:
          return ValidateItemAction(actor, action);

        default:
          Debug.LogWarning($"Unknown action type: {action.ActionType}");
          return false;
      }
    }

    private bool ValidateTarget(ICombatant target) {
      return target != null && target.IsAlive;
    }

    private bool ValidateSkillAction(ICombatant actor, CombatAction action) {
      if (string.IsNullOrEmpty(action.SkillId)) {
        Debug.LogWarning("Skill ID is missing");
        return false;
      }

      SkillComponent skillComponent = actor.GetComponent<SkillComponent>();
      if (skillComponent == null || !skillComponent.CanUseSkill(action.SkillId)) {
        Debug.LogWarning($"Cannot use skill {action.SkillId}");
        return false;
      }

      if (!skillComponent.TryGetSkill(action.SkillId, out CombatSkill combatSkill)) {
        Debug.LogWarning($"Skill {action.SkillId} not learned");
        return false;
      }

      SkillSO skillData = combatSkill.Data;
      List<ICombatant> requestedTargets = SnapshotRequestedTargets(action);
      bool requiresExplicitTarget = skillData.TargetType == TargetType.Single;

      if (requiresExplicitTarget && requestedTargets.Count == 0) {
        Debug.LogWarning($"Skill {action.SkillId} requires a valid target");
        return false;
      }

      foreach (ICombatant requested in requestedTargets) {
        if (!ValidateTarget(requested)) {
          Debug.LogWarning($"Skill target {requested?.Name ?? "<null>"} is no longer valid");
          return false;
        }
      }

      if (_targetResolver == null) {
        return true;
      }

      List<ICombatant> resolvedTargets = _targetResolver.ResolveSkillTargets(actor, skillData, requestedTargets);
      if (resolvedTargets.Count == 0 && skillData.TargetType != TargetType.Self) {
        Debug.LogWarning($"Skill {action.SkillId} has no valid recipients");
        return false;
      }

      return true;
    }

    private bool ValidateItemAction(ICombatant actor, CombatAction action) {
      if (action.ItemData == null) {
        Debug.LogWarning("Item data is missing");
        return false;
      }

      InventoryComponent inventory = actor.GetComponent<InventoryComponent>();
      if (inventory == null || !inventory.HasItem(action.ItemData.ItemId)) {
        Debug.LogWarning($"Cannot use item {action.ItemData.ItemId}");
        return false;
      }

      return action.Target == null || ValidateTarget(action.Target);
    }

    private IEnumerator ProcessActionQueue() {
      _isProcessingAction = true;

      while (_actionQueue.Count > 0) {
        PendingAction pending = _actionQueue.Dequeue();
        yield return ExecuteActionRoutine(pending);
      }

      _isProcessingAction = false;
      _actionQueueRoutine = null;
    }

    private IEnumerator ExecuteActionRoutine(PendingAction pending) {
      ICombatant actor = pending.Actor;
      CombatAction action = pending.Action;

      if (actor == null || !actor.IsAlive) {
        _turnOrderManager.EndCurrentTurn();
        yield break;
      }

      CombatActionTiming timing = _timingProvider.GetTiming(action.ActionType);

      // Validate target again before execution
      if (action.ActionType != CombatActionType.Skill &&
          action.Target != null &&
          !ValidateTarget(action.Target) &&
          action.ActionType != CombatActionType.Defend) {
        OnActionExecuted?.Invoke(actor, ActionResult.Failed("Target no longer valid"));
        _turnOrderManager.EndCurrentTurn();
        yield break;
      }

      // Windup phase
      OnActionPhase?.Invoke(actor, action, CombatActionPhase.Windup);
      if (timing.Windup > 0f) {
        yield return new WaitForSeconds(timing.Windup);
      }

      // Resolution phase
      OnActionPhase?.Invoke(actor, action, CombatActionPhase.Resolve);
      ActionResult result = ProcessAction(actor, action);
      OnActionExecuted?.Invoke(actor, result);

      if (timing.Resolution > 0f) {
        yield return new WaitForSeconds(timing.Resolution);
      }

      // Recovery phase
      OnActionPhase?.Invoke(actor, action, CombatActionPhase.Recovery);
      if (timing.Recovery > 0f) {
        yield return new WaitForSeconds(timing.Recovery);
      }

      _turnOrderManager.EndCurrentTurn();
    }

    private ActionResult ProcessAction(ICombatant actor, CombatAction action) {
      return action.ActionType switch {
        CombatActionType.Attack => ProcessAttackAction(actor, action),
        CombatActionType.Defend => ProcessDefendAction(actor, action),
        CombatActionType.Skill => ProcessSkillAction(actor, action),
        CombatActionType.Item => ProcessItemAction(actor, action),
        _ => ActionResult.Failed("Unknown action type")
      };
    }

    private ActionResult ProcessAttackAction(ICombatant actor, CombatAction action) {
      if (action.Target == null || !action.Target.IsAlive) {
        return ActionResult.Failed("Invalid target");
      }

      int startingHealth = action.Target.GetStat(StatType.Health);
      int damage = _damageCalculator.CalculatePhysicalDamage(actor, action.Target);
      action.Target.TakeDamage(damage);
      int actualDamage = Math.Max(0, startingHealth - action.Target.GetStat(StatType.Health));

      var effects = new List<CombatEffect> {
        new CombatEffect {
          Type = EffectType.Damage,
          Target = action.Target,
          Value = damage,
          AppliedValue = actualDamage
        }
      };

      string message = CombatLogMessageBuilder.BuildActionMessage(
        actor?.Name,
        CombatActionType.Attack.ToString(),
        CombatLogMessageBuilder.BuildEffectSummaries(effects)
      );

      return ActionResult.Success(message, effects);
    }

    private ActionResult ProcessDefendAction(ICombatant actor, CombatAction action) {
      actor.SetDefending(true);

      string message = CombatLogMessageBuilder.BuildActionMessage(
        actor?.Name,
        CombatActionType.Defend.ToString(),
        new List<string> { "is bracing for impact" }
      );

      return ActionResult.Success(message);
    }

    private ActionResult ProcessSkillAction(ICombatant actor, CombatAction action) {
      SkillComponent skillComponent = actor.GetComponent<SkillComponent>();
      if (skillComponent == null) {
        return ActionResult.Failed("Actor has no skills");
      }

      if (!skillComponent.TryGetSkill(action.SkillId, out CombatSkill combatSkill)) {
        return ActionResult.Failed($"Skill {action.SkillId} not known");
      }

      List<ICombatant> requestedTargets = SnapshotRequestedTargets(action);
      List<ICombatant> resolvedTargets = _targetResolver != null
        ? _targetResolver.ResolveSkillTargets(actor, combatSkill.Data, requestedTargets)
        : new List<ICombatant>(requestedTargets);
      action.SetTargets(resolvedTargets);

      SkillResult result = skillComponent.UseSkill(action.SkillId, action.Targets);
      return result.ToActionResult();
    }

    private ActionResult ProcessItemAction(ICombatant actor, CombatAction action) {
      InventoryComponent inventoryComponent = actor.GetComponent<InventoryComponent>();
      return inventoryComponent == null
        ? ActionResult.Failed("Actor has no inventory")
        : inventoryComponent.UseItem(action.ItemData, action.Target).ToActionResult();
    }

    private static List<ICombatant> SnapshotRequestedTargets(CombatAction action) {
      var snapshot = new List<ICombatant>();
      if (action == null) {
        return snapshot;
      }

      if (action.Targets != null) {
        foreach (ICombatant target in action.Targets) {
          if (target != null && !snapshot.Contains(target)) {
            snapshot.Add(target);
          }
        }
      }

      if (snapshot.Count == 0 && action.Target != null) {
        snapshot.Add(action.Target);
      }

      return snapshot;
    }

    private readonly struct PendingAction {
      public ICombatant Actor { get; }
      public CombatAction Action { get; }

      public PendingAction(ICombatant actor, CombatAction action) {
        Actor = actor;
        Action = action;
      }
    }
  }

  /// <summary>
  /// Provides action timing configuration.
  /// </summary>
  public class ActionTimingProvider {
    private readonly CombatActionTiming _defaultTiming;
    private readonly Dictionary<CombatActionType, CombatActionTiming> _overrides = new();

    public ActionTimingProvider(
      CombatActionTiming defaultTiming,
      List<CombatActionTimingOverride> timingOverrides) {
      _defaultTiming = defaultTiming;

      if (timingOverrides != null) {
        foreach (CombatActionTimingOverride timingOverride in timingOverrides) {
          if (timingOverride.Timing.Total > 0f) {
            _overrides[timingOverride.ActionType] = timingOverride.Timing;
          }
        }
      }
    }

    public CombatActionTiming GetTiming(CombatActionType actionType) {
      return _overrides.TryGetValue(actionType, out CombatActionTiming timing)
        ? timing
        : _defaultTiming.Total > 0f ? _defaultTiming : CombatActionTiming.Default;
    }
  }
}
