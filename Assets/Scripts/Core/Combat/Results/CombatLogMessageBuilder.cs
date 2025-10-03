using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Effects;
using EchoesOfTheVoid.Core.Combat.Systems;

namespace EchoesOfTheVoid.Core.Combat.Results {
  public static class CombatLogMessageBuilder {
    public static string BuildActionMessage(string actorName, string actionName, IReadOnlyList<string> effectSummaries) {
      string safeActor = string.IsNullOrWhiteSpace(actorName) ? "Unknown combatant" : actorName;
      string safeAction = string.IsNullOrWhiteSpace(actionName) ? "an action" : actionName;

      if (effectSummaries == null || effectSummaries.Count == 0) {
        return $"{safeActor} used {safeAction}!";
      }

      string effectText = JoinWithAnd(effectSummaries);
      return $"{safeActor} used {safeAction} and {effectText}!";
    }

    public static List<string> BuildEffectSummaries(IEnumerable<CombatEffect> effects) {
      var summaries = new List<string>();
      if (effects == null) {
        return summaries;
      }

      foreach (CombatEffect effect in effects) {
        if (effect == null || effect.Target == null) {
          continue;
        }

        string targetName = string.IsNullOrWhiteSpace(effect.Target.Name) ? "an unknown target" : effect.Target.Name;
        switch (effect.Type) {
          case EffectType.Damage: {
              int amount = effect.AppliedValue > 0 ? effect.AppliedValue : Math.Max(0, effect.Value);
              summaries.Add(amount > 0
                ? $"dealt {amount} damage to {targetName}"
                : $"dealt no damage to {targetName}");
              break;
            }
          case EffectType.Heal: {
              int amount = effect.AppliedValue > 0 ? effect.AppliedValue : Math.Max(0, effect.Value);
              summaries.Add(amount > 0
                ? $"restored {amount} HP to {targetName}"
                : $"restored no HP to {targetName}");
              break;
            }
          case EffectType.ApplyStatus: {
              string statusName = effect.StatusEffect != null && !string.IsNullOrWhiteSpace(effect.StatusEffect.DisplayName)
                ? effect.StatusEffect.DisplayName
                : "a status";
              summaries.Add($"inflicted {statusName} on {targetName}");
              break;
            }
          default:
            break;
        }
      }

      return summaries;
    }

    public static string BuildStatusEffectTickMessage(string targetName, StatusEffect effect, int amount) {
      string safeTarget = string.IsNullOrWhiteSpace(targetName) ? "Unknown combatant" : targetName;
      string effectName = effect != null && !string.IsNullOrWhiteSpace(effect.DisplayName)
        ? effect.DisplayName
        : "a status effect";

      if (effect == null) {
        return $"{safeTarget} is affected by {effectName}.";
      }

      switch (effect.EffectType) {
        case StatusEffectType.DamageOverTime:
          return amount > 0
            ? $"{safeTarget} received {amount} damage from {effectName}!"
            : $"{safeTarget} resisted damage from {effectName}.";
        case StatusEffectType.HealOverTime:
          return amount > 0
            ? $"{safeTarget} recovered {amount} HP from {effectName}!"
            : $"{safeTarget} gained no healing from {effectName}.";
        default:
          return $"{safeTarget} is affected by {effectName}.";
      }
    }

    private static string JoinWithAnd(IReadOnlyList<string> parts) {
      if (parts == null || parts.Count == 0) {
        return string.Empty;
      }

      if (parts.Count == 1) {
        return parts[0];
      }

      if (parts.Count == 2) {
        return $"{parts[0]} and {parts[1]}";
      }

      var buffer = new List<string>(parts);
      string last = buffer[^1];
      buffer.RemoveAt(buffer.Count - 1);
      return string.Join(", ", buffer) + $", and {last}";
    }
  }
}
