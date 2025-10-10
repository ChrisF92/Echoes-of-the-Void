using System;
using UnityEngine;

using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;
using Unity.Properties;

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
    [CreateProperty] public bool IsAutoEnabled { get; private set; }

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
        IsAutoEnabled = false;
        return;
      }

      Name = combatant.Name;
      CurrentHP = combatant.GetStat(StatType.Health);
      MaxHP = combatant.GetMaxStat(StatType.Health);
      IsAlive = combatant.IsAlive;
      IsPlayerControlled = combatant.IsPlayerControlled;
      IsDefending = combatant.IsDefending;
      IsTargetable = combatant.IsAlive;
      IsAutoEnabled = combatant.IsAutoCombatEnabled;

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

    public void SetAutoState(bool isAutoEnabled) {
      IsAutoEnabled = isAutoEnabled;
    }
  }

  [Serializable]
  public class CombatUIData {
    [CreateProperty] public int TurnNumber { get; private set; }
    [CreateProperty] public string BattleTimer { get; private set; } = "00:00";
    [CreateProperty] public string CurrentActionText { get; private set; } = string.Empty;
    [CreateProperty] public string CurrentTurnCharacter { get; private set; } = string.Empty;
    [CreateProperty] public bool IsPlayerTurn { get; private set; }
    [CreateProperty] public string CurrentFloorText { get; private set; } = string.Empty;

    public event Action<int> ValueChanged;

    public void Reset() {
      TurnNumber = 1;
      BattleTimer = "00:00";
      CurrentActionText = string.Empty;
      CurrentTurnCharacter = string.Empty;
      IsPlayerTurn = false;
      CurrentFloorText = string.Empty;
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
      CurrentActionText = actionText ?? string.Empty;
    }

    public void SetBattleTimer(string timerText) {
      BattleTimer = timerText ?? "00:00";
    }

    public void SetFloorInfo(string floorText) {
      CurrentFloorText = floorText ?? string.Empty;
    }
  }
}
