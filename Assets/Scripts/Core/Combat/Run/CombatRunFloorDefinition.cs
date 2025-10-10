using System;
using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Run {
  /// <summary>
  /// Defines the configuration for a single floor within a combat run.
  /// </summary>
  [Serializable]
  public class CombatRunFloorDefinition {
    [SerializeField] private string _floorId;
    [SerializeField] private string _displayName;
    [SerializeField, Min(1)] private int _floorNumber = 1;
    [SerializeField] private List<CombatantSO> _enemyTemplates = new();
    [SerializeField] private CombatRunRewardBundle _rewards = new();
    [SerializeField] private bool _healPartyOnStart;
    [SerializeField, Range(0f, 1f)] private float _playerHealthRestoreRatio;

    public string FloorId => string.IsNullOrWhiteSpace(_floorId) ? $"Floor_{_floorNumber}" : _floorId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? $"Floor {_floorNumber}" : _displayName.Trim();
    public int FloorNumber => Mathf.Max(1, _floorNumber);
    public IReadOnlyList<CombatantSO> EnemyTemplates => _enemyTemplates != null ? _enemyTemplates : Array.Empty<CombatantSO>();
    public CombatRunRewardBundle Rewards => _rewards ?? new CombatRunRewardBundle();
    public bool HealPartyOnStart => _healPartyOnStart;
    public float PlayerHealthRestoreRatio => Mathf.Clamp01(_playerHealthRestoreRatio);

    public bool HasEnemies {
      get {
        if (_enemyTemplates == null) {
          return false;
        }

        for (int i = 0; i < _enemyTemplates.Count; i++) {
          if (_enemyTemplates[i] != null) {
            return true;
          }
        }

        return false;
      }
    }
  }
}
