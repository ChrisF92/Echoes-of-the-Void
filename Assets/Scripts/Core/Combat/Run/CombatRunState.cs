using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Entities;

namespace EchoesOfTheVoid.Core.Combat.Run {
  /// <summary>
  /// Runtime state for a combat run session.
  /// </summary>
  public sealed class CombatRunState {
    private readonly List<Combatant> _playerParty = new();
    private readonly List<CombatRunFloorResult> _floorResults = new();
    private readonly CombatRunRewards _rewards = new();

    public CombatRunDefinition Definition { get; private set; }
    public int CurrentFloorIndex { get; private set; } = -1;
    public bool WasCancelled { get; private set; }
    public IReadOnlyList<Combatant> PlayerParty => _playerParty;
    public IReadOnlyList<CombatRunFloorResult> FloorResults => _floorResults;
    public CombatRunRewards Rewards => _rewards;
    public bool IsActive => Definition != null;

    public CombatRunFloorDefinition CurrentFloor =>
      Definition != null && CurrentFloorIndex >= 0 && CurrentFloorIndex < Definition.FloorCount
        ? Definition.GetFloor(CurrentFloorIndex)
        : null;

    public void Initialize(CombatRunDefinition definition, IEnumerable<Combatant> playerParty) {
      Definition = definition ?? throw new ArgumentNullException(nameof(definition));
      _playerParty.Clear();

      if (playerParty != null) {
        foreach (Combatant combatant in playerParty) {
          if (combatant != null) {
            _playerParty.Add(combatant);
          }
        }
      }

      _floorResults.Clear();
      _rewards.Clear();
      CurrentFloorIndex = -1;
      WasCancelled = false;
    }

    public void Reset() {
      Definition = null;
      _playerParty.Clear();
      _floorResults.Clear();
      _rewards.Clear();
      CurrentFloorIndex = -1;
      WasCancelled = false;
    }

    public CombatRunFloorDefinition AdvanceFloor() {
      if (Definition == null) {
        return null;
      }

      int nextIndex = CurrentFloorIndex + 1;
      if (nextIndex >= Definition.FloorCount) {
        return null;
      }

      CurrentFloorIndex = nextIndex;
      return Definition.GetFloor(CurrentFloorIndex);
    }

    public CombatRunFloorResult RecordFloorResult(
      CombatRunFloorDefinition floor,
      CombatOutcome outcome,
      float durationSeconds,
      int turnCount,
      CombatRunRewards floorRewards,
      IReadOnlyList<CombatRunCombatantSnapshot> playerSnapshots) {

      floor ??= CurrentFloor ?? throw new ArgumentNullException(nameof(floor));

      var result = new CombatRunFloorResult(CurrentFloorIndex, floor, outcome, durationSeconds, turnCount, floorRewards, playerSnapshots);
      _floorResults.Add(result);

      if (floorRewards != null) {
        _rewards.Add(floorRewards);
      }

      return result;
    }

    public void MarkCancelled() {
      WasCancelled = true;
    }

    public bool HasClearedAllFloors => Definition != null && _floorResults.Count >= Definition.FloorCount;

    public List<CombatRunCombatantSnapshot> CapturePlayerSnapshots() {
      var result = new List<CombatRunCombatantSnapshot>(_playerParty.Count);
      foreach (Combatant combatant in _playerParty) {
        result.Add(new CombatRunCombatantSnapshot(combatant));
      }

      return result;
    }
  }
}
