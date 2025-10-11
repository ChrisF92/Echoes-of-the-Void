using System;
using System.Collections.Generic;
using UnityEngine;

using EchoesOfTheVoid.Core.Roster.Progression.Contracts;
using Sirenix.OdinInspector;

namespace EchoesOfTheVoid.Core.Roster.Progression.Definitions {
  [CreateAssetMenu(fileName = "LevelProgression", menuName = "Roster/Progression/Level Progression")]
  public class LevelProgressionDefinition : ScriptableObject, ILevelProgression {
    private const int _minLevelCap = 2;

    [Header("Level Cap")]
    [SerializeField, Min(_minLevelCap)] private int _maxLevel = 50;

    [Header("Experience Formula")]
    [Tooltip("Base experience required to reach level 2.")]
    [SerializeField, Min(1)] private int _baseExperience = 100;

    [Tooltip("Linear growth added per level (starting at level 2).")]
    [SerializeField, Min(0f)] private float _linearGrowth = 15f;

    [Tooltip("Quadratic growth added per level squared. Useful for mid/late-game ramps.")]
    [SerializeField, Min(0f)] private float _quadraticGrowth = 1.25f;

    [Tooltip("Multiplicative factor applied exponentially each level. 1 means no exponential growth.")]
    [SerializeField, MinValue(1f)] private float _growthFactor = 1.07f;

    [Header("Skill Points")]
    [SerializeField, Min(0)] private int _defaultSkillPointsPerLevel = 1;
    [SerializeField] private List<SkillPointOverride> _skillPointOverrides = new();

    [FoldoutGroup("Preview", expanded: false)]
    [PropertyRange(1, "@MaxLevel")]
    [SerializeField] private int _previewLevel = 1;

    [FoldoutGroup("Preview")]
    [ShowInInspector, ReadOnly]
    private int PreviewExperienceRequired => GetExperienceRequiredForLevel(_previewLevel);

    [FoldoutGroup("Preview")]
    [ShowInInspector, ReadOnly]
    private int PreviewTotalExperience => GetTotalExperienceToReachLevel(_previewLevel);

    private readonly Dictionary<int, int> _skillPointLookup = new();

    public int MaxLevel => Mathf.Max(1, _maxLevel);

    private void OnValidate() {
      if (_growthFactor < 1f) {
        _growthFactor = 1f;
      }

      if (_previewLevel < 1) {
        _previewLevel = 1;
      } else if (_previewLevel > MaxLevel) {
        _previewLevel = MaxLevel;
      }

      BuildSkillPointLookup();
    }

    public int GetExperienceRequiredForLevel(int currentLevel) {
      currentLevel = Mathf.Max(1, currentLevel);
      if (IsMaxLevel(currentLevel)) {
        return 0;
      }

      double levelIndex = Math.Max(0, currentLevel - 1);

      double exponential = _baseExperience * Math.Pow(_growthFactor, levelIndex);
      double linear = _linearGrowth * levelIndex;
      double quadratic = _quadraticGrowth * levelIndex * levelIndex;

      double total = exponential + linear + quadratic;
      int required = Mathf.Max(1, (int)Math.Round(total, MidpointRounding.AwayFromZero));
      return required;
    }

    public int GetSkillPointsGrantedAtLevel(int level) {
      level = Mathf.Max(1, level);
      BuildSkillPointLookup();

      if (_skillPointLookup.TryGetValue(level, out int value)) {
        return Mathf.Max(0, value);
      }

      if (level <= 1) {
        return 0;
      }

      return Mathf.Max(0, _defaultSkillPointsPerLevel);
    }

    public bool IsMaxLevel(int level) {
      return level >= MaxLevel;
    }

    private void BuildSkillPointLookup() {
      _skillPointLookup.Clear();
      if (_skillPointOverrides == null) {
        return;
      }

      for (int i = 0; i < _skillPointOverrides.Count; i++) {
        SkillPointOverride entry = _skillPointOverrides[i];
        if (entry == null || entry.Level < 1) {
          continue;
        }

        _skillPointLookup[entry.Level] = Mathf.Max(0, entry.SkillPointsGranted);
      }
    }

    private int GetTotalExperienceToReachLevel(int level) {
      level = Mathf.Max(1, level);
      int total = 0;
      for (int i = 1; i < level; i++) {
        total += GetExperienceRequiredForLevel(i);
      }

      return total;
    }
  }

  [Serializable]
  public class SkillPointOverride {
    [Min(1)] public int Level = 1;
    [Min(0)] public int SkillPointsGranted = 1;
  }
}
