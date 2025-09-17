using System;
using EchoesOfTheVoid.Core;
using UnityEngine;

namespace EchoesOfTheVoid.Combat
{
  /// <summary>
  /// Executes combat actions and reports outcome details via events for UI feedback.
  /// Also optionally advances the turn and clears target highlights.
  /// </summary>
  [DisallowMultipleComponent]
  public sealed class ActionExecutor : MonoBehaviour
  {
    [Header("Systems")]
    [SerializeField] private TurnManager _turnManager;
    [SerializeField] private TargetingSystem _targetingSystem;

    [Header("Behavior")]
    [SerializeField] private bool _autoAdvanceTurn = true;
    [SerializeField] private bool _clearHighlightsAfterAction = true;

    public event Action<ActionExecutionInfo> ActionStarted;
    public event Action<ActionExecutionResult> ActionCompleted;
    public event Action<DamageReport> DamageReported;
    public event Action<HealReport> HealReported;
    public event Action<ResourceChangeReport> ResourceChanged;
    public event Action<StatusReport> StatusApplied;

    /// <summary>
    /// Executes the given action for a user and target. Returns a summary result.
    /// </summary>
    public ActionExecutionResult ExecuteAction(ICombatAction action, ICombatant user, ICombatant target)
    {
      if (action == null) throw new ArgumentNullException(nameof(action));
      if (user == null) throw new ArgumentNullException(nameof(user));

      var info = new ActionExecutionInfo
      {
        Action = action,
        ActionName = action.Name,
        User = user,
        UserName = SafeName(user),
        Target = target,
        TargetName = SafeName(target),
      };

      ActionStarted?.Invoke(info);

      int preTargetHealth = target != null ? target.Health : 0;
      int preUserMana = GetMana(user);

      bool success = true;
      try
      {
        action.Execute(user, target);
      }
      catch (Exception e)
      {
        success = false;
        Debug.LogException(e);
      }

      int postTargetHealth = target != null ? target.Health : preTargetHealth;
      int postUserMana = GetMana(user);

      int damage = 0;
      int heal = 0;
      if (target != null)
      {
        int delta = preTargetHealth - postTargetHealth;
        if (delta > 0)
        {
          damage = delta;
        }
        else if (delta < 0)
        {
          heal = -delta;
        }
      }

      int manaDelta = int.MinValue;
      if (preUserMana != int.MinValue && postUserMana != int.MinValue)
      {
        manaDelta = postUserMana - preUserMana;
      }

      var result = new ActionExecutionResult
      {
        Context = info,
        Success = success,
        DamageDealt = damage,
        HealedAmount = heal,
        ManaDelta = manaDelta == int.MinValue ? 0 : manaDelta,
      };

      if (damage > 0)
      {
        DamageReported?.Invoke(new DamageReport
        {
          Source = user,
          Target = target,
          Action = action,
          Amount = damage,
        });
      }
      if (heal > 0)
      {
        HealReported?.Invoke(new HealReport
        {
          Source = user,
          Target = target,
          Action = action,
          Amount = heal,
        });
      }
      if (manaDelta != int.MinValue && manaDelta != 0)
      {
        ResourceChanged?.Invoke(new ResourceChangeReport
        {
          Actor = user,
          ManaDelta = manaDelta,
        });
      }

      // Basic status inference for provided actions.
      if (action is Actions.DefendAction defend)
      {
        StatusApplied?.Invoke(new StatusReport
        {
          Actor = user,
          Source = action,
          Status = "Defend",
          Turns = defend.Turns,
          Magnitude = defend.DamageReduction,
        });
      }

      ActionCompleted?.Invoke(result);

      // Debug logging of action usage with post-action HP snapshot.
      try
      {
        if (target != null)
        {
          int maxHp = GetMaxHealth(target);
          // Show delta if we inferred damage/heal.
          string delta = damage > 0 ? $" (-{damage})" : (heal > 0 ? $" (+{heal})" : string.Empty);
          Debug.Log($"[Combat] {info.UserName} used {info.ActionName} on {info.TargetName}{delta} | HP {postTargetHealth}/{maxHp}");
        }
        else
        {
          Debug.Log($"[Combat] {info.UserName} used {info.ActionName}");
        }
      }
      catch { /* swallow logging issues */ }

      if (_clearHighlightsAfterAction && _targetingSystem != null)
      {
        _targetingSystem.ClearHighlights();
      }

      if (_autoAdvanceTurn && _turnManager != null && _turnManager.IsCombatActive)
      {
        _turnManager.AdvanceToNextTurn();
      }

      return result;
    }

    /// <summary>
    /// Injects references to optional systems.
    /// </summary>
    public void Configure(TurnManager turnManager, TargetingSystem targetingSystem)
    {
      _turnManager = turnManager;
      _targetingSystem = targetingSystem;
    }

    private static string SafeName(ICombatant c)
    {
      return c != null ? (c.Name ?? string.Empty) : string.Empty;
    }

    private static int GetMana(ICombatant c)
    {
      if (c is IManaUser manaUser)
      {
        return manaUser.Mana;
      }
      return int.MinValue;
    }

    private static int GetMaxHealth(ICombatant c)
    {
      return c != null ? c.MaxHealth : 0;
    }

    public sealed class ActionExecutionInfo
    {
      public ICombatAction Action { get; set; }
      public string ActionName { get; set; }
      public ICombatant User { get; set; }
      public string UserName { get; set; }
      public ICombatant Target { get; set; }
      public string TargetName { get; set; }
    }

    public sealed class ActionExecutionResult
    {
      public ActionExecutionInfo Context { get; set; }
      public bool Success { get; set; }
      public int DamageDealt { get; set; }
      public int HealedAmount { get; set; }
      public int ManaDelta { get; set; }
    }

    public sealed class DamageReport
    {
      public ICombatAction Action { get; set; }
      public ICombatant Source { get; set; }
      public ICombatant Target { get; set; }
      public int Amount { get; set; }
    }

    public sealed class HealReport
    {
      public ICombatAction Action { get; set; }
      public ICombatant Source { get; set; }
      public ICombatant Target { get; set; }
      public int Amount { get; set; }
    }

    public sealed class ResourceChangeReport
    {
      public ICombatant Actor { get; set; }
      public int ManaDelta { get; set; }
    }

    public sealed class StatusReport
    {
      public ICombatant Actor { get; set; }
      public ICombatAction Source { get; set; }
      public string Status { get; set; }
      public int Turns { get; set; }
      public float Magnitude { get; set; }
    }
  }
}
