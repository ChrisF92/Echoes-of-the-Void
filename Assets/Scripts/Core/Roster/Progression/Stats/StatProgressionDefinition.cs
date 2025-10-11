using System.Collections.Generic;
using EchoesOfTheVoid.Core.Combat;
using EchoesOfTheVoid.Core.Combat.Data;
using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Progression.Stats {
  [CreateAssetMenu(
    fileName = "StatProgression",
    menuName = "Roster/Progression/Stat Progression")]
  public class StatProgressionDefinition : ScriptableObject, IStatProgression {
    [SerializeField] private List<StatGrowthBinding> _statGrowth = new();

    private readonly Dictionary<StatType, StatGrowthBinding> _lookup = new();

    private void OnEnable() {
      RebuildLookup();
    }

    private void OnValidate() {
      RebuildLookup();
    }

    public int EvaluateStat(StatType statType, int level, int baseValue) {
      level = Mathf.Max(1, level);
      baseValue = Mathf.Max(0, baseValue);

      StatGrowthBinding binding = GetBinding(statType);
      if (binding == null) {
        return baseValue;
      }

      float result = binding.Evaluate(level, baseValue);
      return Mathf.Max(0, Mathf.RoundToInt(result));
    }

    public CombatStats BuildStatSnapshot(CombatStats baseStats, int level) {
      level = Mathf.Max(1, level);
      CombatStats baseline = baseStats != null ? Clone(baseStats) : new CombatStats();
      return new CombatStats {
        Health = EvaluateStat(StatType.Health, level, baseline.Health),
        Mana = EvaluateStat(StatType.Mana, level, baseline.Mana),
        Attack = EvaluateStat(StatType.Attack, level, baseline.Attack),
        Defense = EvaluateStat(StatType.Defense, level, baseline.Defense),
        Speed = EvaluateStat(StatType.Speed, level, baseline.Speed),
        Luck = EvaluateStat(StatType.Luck, level, baseline.Luck)
      };
    }

    public StatGrowthBinding GetBinding(StatType statType) {
      if (_lookup.TryGetValue(statType, out StatGrowthBinding binding)) {
        return binding;
      }

      if (_statGrowth == null) {
        return null;
      }

      for (int i = 0; i < _statGrowth.Count; i++) {
        StatGrowthBinding entry = _statGrowth[i];
        if (entry == null) {
          continue;
        }

        if (entry.Stat == statType) {
          entry.Validate();
          _lookup[statType] = entry;
          return entry;
        }
      }

      return null;
    }

    public IReadOnlyList<StatGrowthBinding> Bindings => _statGrowth;

    private void RebuildLookup() {
      _lookup.Clear();
      if (_statGrowth == null) {
        _statGrowth = new List<StatGrowthBinding>();
        return;
      }

      for (int i = 0; i < _statGrowth.Count; i++) {
        StatGrowthBinding entry = _statGrowth[i];
        if (entry == null) {
          continue;
        }

        entry.Validate();
        StatType key = entry.Stat;
        if (!_lookup.ContainsKey(key)) {
          _lookup[key] = entry;
        }
      }
    }

    private static CombatStats Clone(CombatStats source) {
      return source != null ? source.Clone() : new CombatStats();
    }
  }
}
