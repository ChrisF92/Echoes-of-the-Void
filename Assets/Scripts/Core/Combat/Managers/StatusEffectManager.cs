using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.Entities;
using EchoesOfTheVoid.Core.Combat;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Systems {
  /// <summary>
  /// Manages all status effects (buffs, debuffs, DoT, HoT) on combatants.
  /// </summary>
  public class StatusEffectManager {
    private readonly Dictionary<ICombatant, List<StatusEffect>> _activeEffects = new();

    public event Action<ICombatant, StatusEffect> OnEffectApplied;
    public event Action<ICombatant, StatusEffect> OnEffectRemoved;
    public event Action<ICombatant, StatusEffect, int> OnEffectTicked;

    /// <summary>
    /// Apply a status effect to a combatant.
    /// </summary>
    public void ApplyEffect(ICombatant target, StatusEffect effect) {
      if (target == null || effect == null) {
        return;
      }

      if (!_activeEffects.ContainsKey(target)) {
        _activeEffects[target] = new List<StatusEffect>();
      }

      // Check for stacking rules
      StatusEffect existing = _activeEffects[target].Find(e => e.Id == effect.Id);
      if (existing != null) {
        switch (effect.StackBehavior) {
          case StackBehavior.Refresh:
            existing.RemainingTurns = effect.Duration;
            break;
          case StackBehavior.Stack:
            existing.StackCount = Mathf.Min(existing.StackCount + 1, effect.MaxStacks);
            existing.RemainingTurns = effect.Duration;
            break;
          case StackBehavior.Extend:
            existing.RemainingTurns += effect.Duration;
            break;
          case StackBehavior.Replace:
            _ = _activeEffects[target].Remove(existing);
            _activeEffects[target].Add(effect.Clone());
            break;
          case StackBehavior.Ignore:
            return;
          default:
            break;
        }
      } else {
        _activeEffects[target].Add(effect.Clone());
      }

      OnEffectApplied?.Invoke(target, effect);
    }

    /// <summary>
    /// Remove a specific effect from a combatant.
    /// </summary>
    public void RemoveEffect(ICombatant target, string effectId) {
      if (!_activeEffects.ContainsKey(target)) {
        return;
      }

      StatusEffect effect = _activeEffects[target].Find(e => e.Id == effectId);
      if (effect != null) {
        _ = _activeEffects[target].Remove(effect);
        OnEffectRemoved?.Invoke(target, effect);
      }
    }

    /// <summary>
    /// Clear all effects from a combatant.
    /// </summary>
    public void ClearEffects(ICombatant target) {
      if (_activeEffects.ContainsKey(target)) {
        _activeEffects[target].Clear();
      }
    }

    /// <summary>
    /// Process effects at turn start.
    /// </summary>
    public void ProcessTurnStart(ICombatant combatant) {
      if (!_activeEffects.ContainsKey(combatant)) {
        return;
      }

      var effects = new List<StatusEffect>(_activeEffects[combatant]);

      foreach (StatusEffect effect in effects) {
        if (effect.TriggerTiming == EffectTriggerTiming.TurnStart) {
          ApplyEffectTick(combatant, effect);
        }
      }
    }

    /// <summary>
    /// Process effects at turn end (most common).
    /// </summary>
    public void ProcessTurnEnd(ICombatant combatant) {
      if (!_activeEffects.ContainsKey(combatant)) {
        return;
      }

      var effects = new List<StatusEffect>(_activeEffects[combatant]);

      foreach (StatusEffect effect in effects) {
        if (effect.TriggerTiming == EffectTriggerTiming.TurnEnd) {
          ApplyEffectTick(combatant, effect);
        }

        // Decrement duration
        effect.RemainingTurns--;
        if (effect.RemainingTurns <= 0) {
          RemoveEffect(combatant, effect.Id);
        }
      }
    }

    private void ApplyEffectTick(ICombatant target, StatusEffect effect) {
      int value = effect.BaseValue * effect.StackCount;
      int appliedAmount = 0;

      switch (effect.EffectType) {
        case StatusEffectType.DamageOverTime: {
            int before = target.GetStat(StatType.Health);
            target.TakeDamage(value);
            int after = target.GetStat(StatType.Health);
            appliedAmount = Math.Max(0, before - after);
            OnEffectTicked?.Invoke(target, effect, appliedAmount);
            break;
          }

        case StatusEffectType.HealOverTime: {
            int before = target.GetStat(StatType.Health);
            target.Heal(value);
            int after = target.GetStat(StatType.Health);
            appliedAmount = Math.Max(0, after - before);
            OnEffectTicked?.Invoke(target, effect, appliedAmount);
            break;
          }

        case StatusEffectType.StatModifier:
          // Stat modifiers are passive, handled during stat queries
          break;
        case StatusEffectType.Stun:
          break;
        case StatusEffectType.Silence:
          break;
        case StatusEffectType.Blind:
          break;
        case StatusEffectType.Custom:
          break;
        default:
          break;
      }
    }

    /// <summary>
    /// Get all active effects on a combatant.
    /// </summary>
    public IReadOnlyList<StatusEffect> GetEffects(ICombatant combatant) {
      return _activeEffects.ContainsKey(combatant)
        ? _activeEffects[combatant]
        : new List<StatusEffect>();
    }

    /// <summary>
    /// Get total stat modifier from all active effects.
    /// </summary>
    public int GetStatModifier(ICombatant combatant, StatType statType) {
      if (!_activeEffects.ContainsKey(combatant)) {
        return 0;
      }

      int totalModifier = 0;
      foreach (StatusEffect effect in _activeEffects[combatant]) {
        if (effect.EffectType == StatusEffectType.StatModifier &&
            effect.TargetStat == statType) {
          totalModifier += effect.BaseValue * effect.StackCount;
        }
      }

      return totalModifier;
    }

    /// <summary>
    /// Check if combatant has a specific effect.
    /// </summary>
    public bool HasEffect(ICombatant combatant, string effectId) {
      return _activeEffects.ContainsKey(combatant) &&
             _activeEffects[combatant].Exists(e => e.Id == effectId);
    }
  }

  /// <summary>
  /// Represents a single status effect instance.
  /// </summary>
  [Serializable]
  public class StatusEffect {
    public string Id;
    public string DisplayName;
    public string Description;
    public Sprite Icon;

    public StatusEffectType EffectType;
    public int BaseValue;
    public StatType TargetStat;

    public int Duration;
    public int RemainingTurns;
    public EffectTriggerTiming TriggerTiming = EffectTriggerTiming.TurnEnd;

    public StackBehavior StackBehavior = StackBehavior.Refresh;
    public int MaxStacks = 1;
    public int StackCount = 1;

    public bool IsDebuff;

    public StatusEffect Clone() {
      return new StatusEffect {
        Id = Id,
        DisplayName = DisplayName,
        Description = Description,
        Icon = Icon,
        EffectType = EffectType,
        BaseValue = BaseValue,
        TargetStat = TargetStat,
        Duration = Duration,
        RemainingTurns = Duration,
        TriggerTiming = TriggerTiming,
        StackBehavior = StackBehavior,
        MaxStacks = MaxStacks,
        StackCount = 1,
        IsDebuff = IsDebuff
      };
    }
  }

  public enum StatusEffectType {
    DamageOverTime,
    HealOverTime,
    StatModifier,
    Stun,
    Silence,
    Blind,
    Custom
  }

  public enum EffectTriggerTiming {
    TurnStart,
    TurnEnd,
    OnHit,
    OnDamaged
  }

  public enum StackBehavior {
    Refresh,  // Reset duration
    Stack,    // Add stack, reset duration
    Extend,   // Add to duration
    Replace,  // Remove old, add new
    Ignore    // Don't apply if exists
  }
}