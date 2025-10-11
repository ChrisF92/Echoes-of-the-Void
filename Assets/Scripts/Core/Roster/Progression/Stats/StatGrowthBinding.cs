using EchoesOfTheVoid.Core.Combat;
using UnityEngine;

namespace EchoesOfTheVoid.Core.Roster.Progression.Stats {
  [System.Serializable]
  public class StatGrowthBinding {
    [SerializeField] private StatType _stat;
    [SerializeField] private StatFormulaConfig _formula;
    [SerializeField] private StatCombinationMode _combinationMode = StatCombinationMode.Additive;
    [SerializeField, Min(0f)] private float _minimumValue = 0f;

    public StatType Stat => _stat;
    public StatFormulaConfig Formula => _formula;
    public StatCombinationMode CombinationMode => _combinationMode;

    public float Evaluate(int level, float baseValue) {
      if (level <= 1) {
        return Mathf.Max(_minimumValue, baseValue);
      }

      float raw = _formula.Evaluate(level, baseValue);
      float combined = Combine(baseValue, raw, _combinationMode, _formula.FormulaType);
      return Mathf.Max(_minimumValue, combined);
    }

    public void Validate() {
      _formula = _formula.Validate();
    }

    private static float Combine(float baseValue, float rawValue, StatCombinationMode mode, StatFormulaType formulaType) {
      return mode switch {
        StatCombinationMode.Additive => baseValue + rawValue,
        StatCombinationMode.Multiplicative => baseValue * GetMultiplier(rawValue, formulaType),
        StatCombinationMode.Hybrid => baseValue + (baseValue * rawValue),
        StatCombinationMode.Replace => rawValue,
        _ => baseValue
      };
    }

    private static float GetMultiplier(float rawValue, StatFormulaType formulaType) {
      if (formulaType == StatFormulaType.None) {
        return 1f;
      }

      if (formulaType == StatFormulaType.Constant && Mathf.Approximately(rawValue, 0f)) {
        return 1f;
      }

      return rawValue;
    }
  }
}
