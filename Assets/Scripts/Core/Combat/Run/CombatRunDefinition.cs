using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Run {
  [CreateAssetMenu(fileName = "CombatRun", menuName = "Combat/Run Definition")]
  public sealed class CombatRunDefinition : ScriptableObject {
    [SerializeField] private string _runId;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;
    [SerializeField] private List<CombatRunFloorDefinition> _floors = new();
    [SerializeField] private CombatRunRewardBundle _completionRewards = new();

    public string RunId => string.IsNullOrWhiteSpace(_runId) ? name : _runId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName.Trim();
    public string Description => _description;
    public Sprite Icon => _icon;
    public IReadOnlyList<CombatRunFloorDefinition> Floors => _floors != null ? _floors : Array.Empty<CombatRunFloorDefinition>();
    public CombatRunRewardBundle CompletionRewards => _completionRewards ?? new CombatRunRewardBundle();
    public int FloorCount => _floors?.Count ?? 0;

    public CombatRunFloorDefinition GetFloor(int index) {
      if (_floors == null || index < 0 || index >= _floors.Count) {
        return null;
      }

      return _floors[index];
    }

    public int IndexOfFloor(string floorId) {
      if (string.IsNullOrWhiteSpace(floorId) || _floors == null) {
        return -1;
      }

      for (int i = 0; i < _floors.Count; i++) {
        CombatRunFloorDefinition floor = _floors[i];
        if (floor != null && string.Equals(floor.FloorId, floorId, StringComparison.OrdinalIgnoreCase)) {
          return i;
        }
      }

      return -1;
    }

    private void OnValidate() {
      if (_floors == null) {
        _floors = new List<CombatRunFloorDefinition>();
      }

      for (int i = 0; i < _floors.Count; i++) {
        CombatRunFloorDefinition floor = _floors[i];
        if (floor == null) {
          _floors[i] = new CombatRunFloorDefinition();
        }
      }
    }
  }
}
