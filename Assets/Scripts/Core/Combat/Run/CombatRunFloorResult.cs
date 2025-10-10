using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;

namespace EchoesOfTheVoid.Core.Combat.Run {
  /// <summary>
  /// Captures the outcome of a single floor during a combat run.
  /// </summary>
  public sealed class CombatRunFloorResult {
    public CombatRunFloorResult(
      int floorIndex,
      CombatRunFloorDefinition definition,
      CombatOutcome outcome,
      float durationSeconds,
      int turnCount,
      CombatRunRewards rewards,
      IReadOnlyList<CombatRunCombatantSnapshot> playerSnapshots) {

      FloorIndex = floorIndex;
      Definition = definition ?? throw new ArgumentNullException(nameof(definition));
      Outcome = outcome;
      DurationSeconds = durationSeconds;
      TurnCount = Math.Max(0, turnCount);

      Rewards = new CombatRunRewards();
      if (rewards != null) {
        Rewards.Add(rewards);
      }

      PlayerSnapshots = playerSnapshots ?? Array.Empty<CombatRunCombatantSnapshot>();
    }

    public int FloorIndex { get; }
    public CombatRunFloorDefinition Definition { get; }
    public CombatOutcome Outcome { get; }
    public float DurationSeconds { get; }
    public int TurnCount { get; }
    public CombatRunRewards Rewards { get; }
    public IReadOnlyList<CombatRunCombatantSnapshot> PlayerSnapshots { get; }
  }
}
