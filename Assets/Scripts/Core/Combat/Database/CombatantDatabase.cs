using System;
using System.Collections.Generic;
using System.Linq;
using EchoesOfTheVoid.Core.Combat.ScriptableObjects;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Combat.Database {
  [DisallowMultipleComponent]
  public class CombatantDatabase : MonoBehaviour {
    public static CombatantDatabase Instance { get; private set; }

    [SerializeField] private List<CombatantSO> _combatants = new();

    private readonly Dictionary<string, CombatantSO> _lookup = new(StringComparer.Ordinal);

    private void Awake() {
      if (Instance == null) {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
      } else if (Instance != this) {
        Destroy(gameObject);
      }
    }

    private void OnValidate() {
      RebuildLookup();
    }

    public CombatantSO GetCombatant(string combatantId) {
      if (string.IsNullOrWhiteSpace(combatantId)) {
        return null;
      }

      return _lookup.TryGetValue(combatantId, out CombatantSO combatant) ? combatant : null;
    }

    public IEnumerable<CombatantSO> GetCombatants() {
      return _combatants.Where(static combatant => combatant != null);
    }

    public void RegisterCombatant(CombatantSO combatant) {
      if (combatant == null || string.IsNullOrWhiteSpace(combatant.CombatantId)) {
        return;
      }

      if (!_combatants.Contains(combatant)) {
        _combatants.Add(combatant);
      }

      _lookup[combatant.CombatantId] = combatant;
    }

    public void UnregisterCombatant(CombatantSO combatant) {
      if (combatant == null) {
        return;
      }

      _ = _combatants.Remove(combatant);
      if (!string.IsNullOrWhiteSpace(combatant.CombatantId)) {
        _ = _lookup.Remove(combatant.CombatantId);
      }
    }

    private void RebuildLookup() {
      _lookup.Clear();

      for (int i = _combatants.Count - 1; i >= 0; i--) {
        CombatantSO entry = _combatants[i];
        if (entry == null) {
          _combatants.RemoveAt(i);
          continue;
        }

        if (string.IsNullOrWhiteSpace(entry.CombatantId)) {
          continue;
        }

        _lookup[entry.CombatantId] = entry;
      }
    }
  }
}
